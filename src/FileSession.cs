using System;using System.IO;using System.Text;using System.Security.Cryptography;using System.Threading;using System.Collections.Generic;using System.Text.RegularExpressions;
namespace MrDaktilo.Files {
public sealed class Entry {public string Name,Path;public bool Directory;public long Bytes;}
public sealed class FileSession:IDisposable {
 const int Chunk=16384,PageSize=200;const long CreditWindow=524288;const long MaxFile=1099511627776L;
 readonly Action<byte[]> send;readonly bool host;readonly object gate=new object();Transfer current;int serial,latestListId;volatile bool closed,abortRequested;bool listRunning;readonly System.Threading.Timer watchdog;public event Action<string> Status;public event Action<long,long> Progress;public event Action<string,int,bool,List<Entry>> Listed;
 enum TransferPhase {Opening,Receiving,Sending,Committing,Committed,Cancelled,Failed}
 sealed class Transfer:IDisposable {
  public readonly object Sync=new object();public long Activity=DateTime.UtcNow.Ticks;public int Id,Sequence;public long Length,Done;public bool Receiver,Overwrite,Success,EndSent,TempOwned,Disposed;public volatile bool Cancelled;public string Target,Temp,Error,CleanupError;public FileStream Stream;public SHA256 Hash;public TransferPhase Phase=TransferPhase.Opening;
  public long Acked;public readonly AutoResetEvent Credit=new AutoResetEvent(false);public readonly ManualResetEvent Ready=new ManualResetEvent(false),Finished=new ManualResetEvent(false);
  public void Dispose(){lock(Sync){if(Disposed)return;try{if(Stream!=null)Stream.Dispose();}catch(Exception e){CleanupError=e.Message;}Stream=null;try{if(Hash!=null)Hash.Dispose();}catch{}Hash=null;
   // Only a successfully created staging file is owned. Committed targets are never cleanup candidates.
   if(Phase!=TransferPhase.Committed&&TempOwned&&Temp!=null){try{File.Delete(Temp);Temp=null;TempOwned=false;}catch(Exception e){CleanupError="Geçici dosya temizlenemedi: "+Temp+" ("+e.Message+")";}}
   Ready.Set();Finished.Set();Credit.Set();Ready.Dispose();Finished.Dispose();Credit.Dispose();Disposed=true;
  }}
 }

 public FileSession(bool receiverHost,Action<byte[]> sender){host=receiverHost;send=sender;watchdog=new System.Threading.Timer(o=>{Transfer t;lock(gate)t=current;if(t!=null&&(DateTime.UtcNow-new DateTime(Interlocked.Read(ref t.Activity),DateTimeKind.Utc)).TotalSeconds>90){Say("Aktarım durdu: 90 saniyedir dosya verisi alınmadı.");Cancel();}},null,10000,10000);}
 public bool Busy{get{lock(gate)return current!=null&&!abortRequested;}}
 static void Str(BinaryWriter w,string s){byte[] b=Encoding.UTF8.GetBytes(s??"");if(b.Length>4096)throw new IOException("Yol veya metin çok uzun.");w.Write(b.Length);w.Write(b);}
 static string Str(BinaryReader r){int n=r.ReadInt32();if(n<0||n>4096)throw new IOException("Geçersiz metin boyutu.");byte[] b=r.ReadBytes(n);if(b.Length!=n)throw new EndOfStreamException();return new UTF8Encoding(false,true).GetString(b);}
 static byte[] Packet(byte type,int id,Action<BinaryWriter> write){using(var m=new MemoryStream()){using(var w=new BinaryWriter(m,Encoding.UTF8)){w.Write(type);w.Write(id);if(write!=null)write(w);w.Flush();return m.ToArray();}}}
 void Send(byte type,int id,Action<BinaryWriter> write){if(closed||abortRequested)throw new IOException("Dosya oturumu kapalı.");send(Packet(type,id,write));}
 void Say(string text){var e=Status;if(e!=null)foreach(Action<string> callback in e.GetInvocationList())try{callback(text);}catch(Exception error){System.Diagnostics.Trace.TraceError("File status callback failed: "+error.GetType().Name);}}void Tick(long a,long b){var e=Progress;if(e!=null)foreach(Action<long,long> callback in e.GetInvocationList())try{callback(a,b);}catch(Exception error){System.Diagnostics.Trace.TraceError("File progress callback failed: "+error.GetType().Name);}}
 static void Start(Action action){var t=new Thread(()=>action());t.IsBackground=true;t.Start();}
 static string HashText(byte[] b){return BitConverter.ToString(b).Replace("-","");}
 public static string LocalPath(string path){if(string.IsNullOrWhiteSpace(path)||path.Length>2048||path.IndexOf('\0')>=0)throw new IOException("Geçersiz dosya yolu.");path=path.Replace('/','\\');if(!Regex.IsMatch(path,@"^[A-Za-z]:\\")||path.Substring(2).Contains(":"))throw new IOException("Yalnızca yerel Windows sürücü yolları destekleniyor.");foreach(char c in Path.GetInvalidPathChars())if(path.IndexOf(c)>=0)throw new IOException("Geçersiz yol karakteri.");if(path.Contains("*")||path.Contains("?"))throw new IOException("Joker karakter desteklenmiyor.");string full=Path.GetFullPath(path);foreach(string segment in full.Substring(3).Split('\\')){if(segment.Length==0)continue;if(segment!=segment.TrimEnd(' ','.')||Regex.IsMatch(segment,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\..*)?$",RegexOptions.IgnoreCase))throw new IOException("Aygıt adı dosya yolu olarak kullanılamaz.");}return full;}
 static string Name(string name){if(string.IsNullOrWhiteSpace(name)||name=="."||name==".."||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||name!=Path.GetFileName(name)||name!=name.TrimEnd(' ','.')||Regex.IsMatch(name,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\..*)?$",RegexOptions.IgnoreCase))throw new IOException("Geçersiz dosya adı.");return name;}
 void Begin(Transfer t){lock(gate){if(closed||abortRequested)throw new IOException("Oturum kapalı.");if(current!=null)throw new IOException("Önce mevcut dosya aktarımını tamamlayın veya iptal edin.");current=t;}}
 Transfer Find(int id){lock(gate)return current!=null&&current.Id==id?current:null;}
 void Release(Transfer t){t.Dispose();lock(gate){if(object.ReferenceEquals(current,t))current=null;}if(t.CleanupError!=null)Say("Uyarı: "+t.CleanupError);}
 bool MarkStopped(Transfer t,string error,bool failed){lock(t.Sync){if(t.Disposed||t.Phase==TransferPhase.Committed||t.Phase==TransferPhase.Failed||t.Phase==TransferPhase.Cancelled)return false;t.Error=error;t.Cancelled=true;t.Phase=failed?TransferPhase.Failed:TransferPhase.Cancelled;t.Ready.Set();t.Finished.Set();t.Credit.Set();return true;}}
 static void CheckSender(Transfer t){lock(t.Sync){if(t.Phase==TransferPhase.Failed)throw new IOException(t.Error??"Aktarım başarısız.");if(t.Cancelled||t.Disposed)throw new OperationCanceledException();}}

 void Error(int id,string message){if(id<=0)return;try{Send(69,id,w=>Str(w,message.Length>500?message.Substring(0,500):message));}catch{}}
 void ListHost(int id,string path,int page){bool accepted;lock(gate){accepted=!listRunning;if(accepted)listRunning=true;}if(!accepted){Error(id,"Önceki klasör isteği sürüyor.");return;}Start(()=>{try{if(page<0||page>1000000)throw new IOException("Geçersiz sayfa.");var entries=new List<Entry>();bool more=false;if(path=="@drives"){foreach(var drive in DriveInfo.GetDrives()){entries.Add(new Entry{Name=drive.Name,Path=drive.Name,Directory=true,Bytes=0});}path="@drives";}else{if(string.IsNullOrWhiteSpace(path)){path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads");if(!Directory.Exists(path))path=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);}path=LocalPath(path);int skip=checked(page*PageSize),index=0;foreach(string item in Directory.EnumerateFileSystemEntries(path)){if(index++<skip)continue;if(entries.Count>=PageSize){more=true;break;}try{var attr=File.GetAttributes(item);bool dir=(attr&FileAttributes.Directory)!=0;entries.Add(new Entry{Name=Path.GetFileName(item),Path=item,Directory=dir,Bytes=dir?0:new FileInfo(item).Length});}catch(UnauthorizedAccessException){}catch(IOException){}}}
 Send(61,id,w=>{Str(w,path);w.Write(page);w.Write(more);w.Write(entries.Count);foreach(var item in entries){Str(w,item.Name);Str(w,item.Path);w.Write(item.Directory);w.Write(item.Bytes);}});}catch(Exception e){Error(id,e.Message);}finally{lock(gate)listRunning=false;}});}
 void SendFile(Transfer t,string source,bool upload,string folder,bool overwrite){try{
  lock(t.Sync){if(t.Cancelled)throw new OperationCanceledException();t.Phase=TransferPhase.Sending;}
  using(var stream=new FileStream(LocalPath(source),FileMode.Open,FileAccess.Read,FileShare.Read,Chunk,FileOptions.SequentialScan))using(var hash=SHA256.Create()){
   t.Length=stream.Length;if(t.Length<0||t.Length>MaxFile)throw new IOException("Dosya boyutu desteklenen sınırın dışında.");
   if(upload){Send(62,t.Id,w=>{Str(w,folder);Str(w,Name(Path.GetFileName(source)));w.Write(t.Length);w.Write(overwrite);});if(!t.Ready.WaitOne(30000))throw new IOException("Client dosya isteğine yanıt vermedi.");CheckSender(t);}else Send(67,t.Id,w=>{Str(w,Name(Path.GetFileName(source)));w.Write(t.Length);});
   CheckSender(t);Say("Gönderiliyor: "+Path.GetFileName(source));byte[] data=new byte[Chunk];
   while(t.Done<t.Length){CheckSender(t);while(t.Done-Interlocked.Read(ref t.Acked)>=CreditWindow){CheckSender(t);if(closed||abortRequested)throw new OperationCanceledException();t.Credit.WaitOne(200);}CheckSender(t);if(closed)throw new OperationCanceledException();int n=stream.Read(data,0,(int)Math.Min(Chunk,t.Length-t.Done));if(n<=0)throw new EndOfStreamException("Kaynak dosya tamamlanamadı.");hash.TransformBlock(data,0,n,null,0);int sequence=t.Sequence++;Send(63,t.Id,w=>{w.Write(sequence);w.Write(n);w.Write(data,0,n);});t.Done+=n;Interlocked.Exchange(ref t.Activity,DateTime.UtcNow.Ticks);Tick(t.Done,t.Length);}
   hash.TransformFinalBlock(new byte[0],0,0);CheckSender(t);string digest=HashText(hash.Hash);
   lock(t.Sync){CheckSender(t);t.EndSent=true;}
   Send(64,t.Id,w=>Str(w,digest));if(!t.Finished.WaitOne(30000))throw new IOException("Tamamlanma onayı alınamadı.");
   lock(t.Sync){if(!t.Success){if(t.Phase==TransferPhase.Failed)throw new IOException(t.Error??"Aktarım başarısız.");throw new OperationCanceledException();}}
   Say("Tamamlandı: "+Path.GetFileName(source));
  }
 }catch(OperationCanceledException){bool unknown;lock(t.Sync)unknown=t.EndSent&&!t.Success;Say(unknown?"Sonuç belirsiz: dosya karşı tarafta kaydedilmiş olabilir; yeniden göndermeden hedefi kontrol edin.":"Aktarım iptal edildi.");try{Send(68,t.Id,null);}catch{}}
 catch(Exception e){bool unknown;lock(t.Sync)unknown=t.EndSent&&!t.Success&&t.Error==null;if(unknown)Say("Sonuç belirsiz: tamamlanma onayı alınamadı; dosya karşı tarafta kaydedilmiş olabilir. Hedefi kontrol edin.");else{Say("Hata: "+e.Message);Error(t.Id,e.Message);}}
 finally{Release(t);}}
 void OpenReceiver(Transfer t,long length){lock(t.Sync){if(t.Disposed||t.Cancelled)throw new OperationCanceledException();if(length<0||length>MaxFile)throw new IOException("Geçersiz dosya boyutu.");t.Length=length;t.Target=LocalPath(t.Target);string dir=Path.GetDirectoryName(t.Target);if(!Directory.Exists(dir))throw new IOException("Hedef klasör bulunamadı.");if(File.Exists(t.Target)&&!t.Overwrite)throw new IOException("Hedefte aynı adlı dosya var. Üzerine yazma onayı gerekir.");
  string temp=Path.Combine(dir,".Daktilo-"+Guid.NewGuid().ToString("N")+".part");var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,Chunk,FileOptions.SequentialScan);t.Stream=stream;t.Temp=temp;t.TempOwned=true;t.Hash=SHA256.Create();t.Phase=TransferPhase.Receiving;
 }Say("Alınıyor: "+Path.GetFileName(t.Target));Tick(0,length);}

 public bool Handle(byte[] bytes){if(bytes==null||bytes.Length==0||bytes[0]<60||bytes[0]>69)return false;int id=0;if(abortRequested)return true;
 try{if(bytes.Length<5||bytes.Length>2097152)throw new IOException("Geçersiz dosya paketi.");using(var memory=new MemoryStream(bytes))using(var r=new BinaryReader(memory,Encoding.UTF8)){
  byte kind=r.ReadByte();id=r.ReadInt32();if(id<=0)throw new IOException("Geçersiz aktarım kimliği.");
  if(kind==60&&host){string path=Str(r);int page=r.ReadInt32();ListHost(id,path,page);return true;}
  if(kind==61&&!host){if(id!=latestListId)return true;string path=Str(r);int page=r.ReadInt32();bool more=r.ReadBoolean();int n=r.ReadInt32();if(n<0||n>PageSize+32)throw new IOException("Geçersiz liste boyutu.");var rows=new List<Entry>();for(int i=0;i<n;i++)rows.Add(new Entry{Name=Str(r),Path=Str(r),Directory=r.ReadBoolean(),Bytes=r.ReadInt64()});var callback=Listed;if(callback!=null)callback(path,page,more,rows);return true;}
  if(kind==62&&host){string folder=LocalPath(Str(r)),name=Name(Str(r));long length=r.ReadInt64();bool overwrite=r.ReadBoolean();var t=new Transfer{Id=id,Receiver=true,Target=Path.Combine(folder,name),Overwrite=overwrite};Begin(t);try{OpenReceiver(t,length);Send(65,id,w=>w.Write((byte)1));}catch{MarkStopped(t,"Dosya alımı başlatılamadı.",true);Release(t);throw;}return true;}
  if(kind==66&&host){string source=LocalPath(Str(r));var t=new Transfer{Id=id,Receiver=false};Begin(t);Start(()=>SendFile(t,source,false,null,false));return true;}
  Transfer transfer=Find(id);if(transfer!=null)Interlocked.Exchange(ref transfer.Activity,DateTime.UtcNow.Ticks);
  if(kind==69){string error=Str(r);bool applied=transfer!=null&&MarkStopped(transfer,error,true);if(applied&&transfer.Receiver)Release(transfer);if(transfer==null||applied)Say("Hata: "+error);return true;}
  if(transfer==null)return true;
  if(kind==67&&!host&&transfer.Receiver){Name(Str(r));OpenReceiver(transfer,r.ReadInt64());return true;}
  if(kind==63&&transfer.Receiver){int sequence=r.ReadInt32(),n=r.ReadInt32();if(n<0||n>Chunk)throw new IOException("Geçersiz dosya parçası boyutu.");byte[] data=r.ReadBytes(n);if(data.Length!=n)throw new EndOfStreamException();long done,total;
   lock(transfer.Sync){if(transfer.Disposed||transfer.Cancelled||transfer.Phase==TransferPhase.Committed)return true;if(transfer.Phase!=TransferPhase.Receiving||transfer.Stream==null||sequence!=transfer.Sequence||n>transfer.Length-transfer.Done)throw new IOException("Dosya parçası sırası veya boyutu geçersiz.");transfer.Stream.Write(data,0,n);transfer.Hash.TransformBlock(data,0,n,null,0);transfer.Sequence++;transfer.Done+=n;done=transfer.Done;total=transfer.Length;}
   Tick(done,total);Send(65,id,w=>{w.Write((byte)3);w.Write(done);});return true;
  }
  if(kind==64&&transfer.Receiver){string expected=Str(r);long length;string target;
   lock(transfer.Sync){if(transfer.Disposed||transfer.Cancelled||transfer.Phase==TransferPhase.Committed)return true;if(transfer.Phase!=TransferPhase.Receiving||transfer.Stream==null||transfer.Done!=transfer.Length)throw new IOException("Dosya eksik alındı.");transfer.Hash.TransformFinalBlock(new byte[0],0,0);if(!string.Equals(expected,HashText(transfer.Hash.Hash),StringComparison.OrdinalIgnoreCase))throw new IOException("Dosya özeti eşleşmedi.");transfer.Phase=TransferPhase.Committing;
    // Commit and cancellation have a deterministic winner: the holder of this lock completes first.
    transfer.Stream.Flush(true);transfer.Stream.Dispose();transfer.Stream=null;
    if(File.Exists(transfer.Target)){if(!transfer.Overwrite)throw new IOException("Hedef dosya oluşturulmuş; üzerine yazılmadı.");File.Replace(transfer.Temp,transfer.Target,null);}else File.Move(transfer.Temp,transfer.Target);
    transfer.Temp=null;transfer.TempOwned=false;transfer.Success=true;transfer.Phase=TransferPhase.Committed;length=transfer.Length;target=transfer.Target;
   }
   bool acknowledged=true;try{Send(65,id,w=>w.Write((byte)2));}catch{acknowledged=false;}
   Say(acknowledged?"Tamamlandı: "+Path.GetFileName(target):"Dosya kaydedildi; tamamlanma onayı gönderilemedi. Karşı tarafta sonuç belirsiz olabilir.");Tick(length,length);Release(transfer);return true;
  }
  if(kind==65&&!transfer.Receiver){byte state=r.ReadByte();if(state==3){long ack=r.ReadInt64();lock(transfer.Sync){if(!transfer.Disposed&&ack>=transfer.Acked&&ack<=transfer.Length){Interlocked.Exchange(ref transfer.Acked,ack);transfer.Credit.Set();}}return true;}lock(transfer.Sync){if(transfer.Disposed)return true;if(state==1){if(!transfer.Cancelled)transfer.Ready.Set();}else if(state==2){if(transfer.EndSent){transfer.Success=true;transfer.Cancelled=false;transfer.Phase=TransferPhase.Committed;transfer.Finished.Set();}}else throw new IOException("Geçersiz dosya yanıtı.");}return true;}
  if(kind==68){bool applied=MarkStopped(transfer,"Aktarım iptal edildi.",false);if(applied&&transfer.Receiver)Release(transfer);if(applied)Say((transfer.EndSent&&!transfer.Receiver?"Sonuç belirsiz: iptal isteği tamamlanma sırasında geldi; hedef dosyayı kontrol edin.":"Aktarım iptal edildi.")+(transfer.CleanupError==null?"":" Uyarı: "+transfer.CleanupError));return true;}
  throw new IOException("Dosya mesajı bu işlemde kullanılamaz.");
 }}catch(OperationCanceledException){var t=Find(id);if(t!=null&&t.Receiver)Release(t);}
 catch(Exception error){var t=Find(id);bool applied=t==null||MarkStopped(t,error.Message,true);if(applied){Error(id,error.Message);if(t!=null&&t.Receiver)Release(t);Say("Hata: "+error.Message+(t==null||t.CleanupError==null?"":" Uyarı: "+t.CleanupError));}}
 return true;}
 public void Cancel(){Transfer t;lock(gate)t=current;if(t==null)return;bool applied=MarkStopped(t,"Aktarım iptal edildi.",false);if(!applied)return;bool unknown;lock(t.Sync)unknown=t.EndSent&&!t.Receiver;if(t.Receiver)Release(t);try{Send(68,t.Id,null);}catch{}Say((unknown?"Sonuç belirsiz: iptal tamamlanma sırasında istendi; hedef dosyayı kontrol edin.":"Aktarım iptal edildi.")+(t.CleanupError==null?"":" Uyarı: "+t.CleanupError));}
 public void RequestAbort(){abortRequested=true;Transfer t;lock(gate)t=current;if(t!=null){t.Cancelled=true;try{t.Credit.Set();}catch(ObjectDisposedException){}}}
 public void Dispose(){if(closed)return;closed=true;watchdog.Dispose();Cancel();Transfer t;lock(gate){t=current;if(t!=null&&t.Receiver)current=null;}if(t!=null&&t.Receiver)t.Dispose();}

}
}
