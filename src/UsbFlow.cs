using System;using System.IO;using System.Text;using System.Reflection;using System.Collections.Generic;using System.Collections.Concurrent;using System.Net.Sockets;using System.Threading;using System.Diagnostics;using System.Text.RegularExpressions;
namespace MrDaktilo.Performance {
 public static class UsbFlow {
 const int MaxFlows=16,MaxQueuedBytes=262144,MaxQueuedFrames=32,MaxPayload=65536;
 static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object,State> states=new System.Runtime.CompilerServices.ConditionalWeakTable<object,State>();
 static object Field(object target,string name){var f=target.GetType().GetField(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);if(f==null)throw new MissingFieldException(target.GetType().FullName,name);return f.GetValue(target);}
 static object Invoke(object target,string name,params object[] args){var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;var m=target.GetType().GetMethod(name,flags);if(m==null)throw new MissingMethodException(target.GetType().FullName,name);try{return m.Invoke(m.IsStatic?null:target,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
 static State Get(object bridge){return states.GetValue(bridge,b=>new State(b));}
 public static bool Handle(object bridge,byte[] frame){if(frame==null||frame.Length==0)return false;byte kind=frame[0];if(kind!=40&&kind!=41&&kind!=42&&kind!=43&&kind!=45)return false;State state=Get(bridge);if(state.Disposed)return true;if(frame.Length<5){state.Report("USB protokol hatası: kısa paket.");return true;}int id=BitConverter.ToInt32(frame,1);
 if(kind==43||kind==45){if(!state.Host)return false;if((kind==43&&frame.Length!=5)||(kind==45&&frame.Length>32)){state.Report("USB denetim paketi geçersiz.");return true;}if(!state.Controls.Add(()=>state.Command(frame)))state.Reply(46,0,"USB denetim kuyruğu dolu; isteği yeniden deneyin.");return true;}
 if(id<=0){state.Report("USB protokol hatası: geçersiz bağlantı kimliği.");return true;}
 if((kind==40||kind==42)&&frame.Length!=5||kind==41&&frame.Length>MaxPayload+5){state.AbortId(id,"USB paketi boyut sınırını aştı.",true);return true;}
 if(kind==40&&!state.Host){state.AbortId(id,"USB açma isteği bu uçta kabul edilmedi.",true);return true;}
 Flow flow;lock(state.Gate){if(state.Disposed)return true;if(state.Closed.Contains(id))return true;if(!state.Flows.TryGetValue(id,out flow)){if(kind==42){state.RememberClosed(id);flow=null;}else if(state.Flows.Count>=MaxFlows){state.RememberClosed(id);flow=null;}else if(kind==41&&!state.HasLink(id)){state.RememberClosed(id);flow=null;}else{flow=new Flow(state,id);state.Flows.Add(id,flow);}}}
 if(flow==null){state.CloseLink(id);if(kind!=42)state.NotifyClose(id,"USB bağlantısı kabul edilmedi veya artık açık değil.");return true;}
 if(!flow.Add(frame))state.AbortId(id,"USB tüketicisi yetişemedi: 256 KiB/32 paket sınırı aşıldı; bu USB bağlantısı kapatıldı.",true);return true;}
 public static void Dispose(object bridge){Get(bridge).Shutdown();}
 sealed class State {
 public readonly object Bridge,Gate=new object();public readonly bool Host;public readonly Dictionary<int,Flow> Flows=new Dictionary<int,Flow>();public readonly HashSet<int> Closed=new HashSet<int>();readonly Queue<int> closedOrder=new Queue<int>();public volatile bool Disposed;public readonly WorkQueue Controls,Notifications;int detaching;
 public State(object bridge){Bridge=bridge;Host=(bool)Field(bridge,"host");Controls=new WorkQueue(8);Notifications=new WorkQueue(32);}
 public bool HasLink(int id){var links=Field(Bridge,"links") as ConcurrentDictionary<int,TcpClient>;return links!=null&&links.ContainsKey(id);}
 public void RememberClosed(int id){if(Closed.Add(id)){closedOrder.Enqueue(id);while(closedOrder.Count>256)Closed.Remove(closedOrder.Dequeue());}}
 public void CloseLink(int id){try{Invoke(Bridge,"CloseLink",id);}catch(Exception e){Report("USB kapatma hatası: "+e.Message);}}
 public void Report(string text){try{Invoke(Bridge,"Say",text);}catch{}}
 public void Reply(byte kind,int id,string text){if(Disposed)return;if(!Notifications.Add(()=>{if(!Disposed)try{Invoke(Bridge,"Send",kind,id,Encoding.UTF8.GetBytes(text));}catch(Exception e){Report("USB yanıtı gönderilemedi: "+e.Message);}}))Report("USB bildirim kuyruğu dolu; işlem sonucu iletilemedi.");}
 public void NotifyClose(int id,string reason){Report(reason);if(Disposed)return;if(!Notifications.Add(()=>{if(!Disposed)try{Invoke(Bridge,"Send",(byte)42,id,new byte[0]);}catch(Exception e){Report("USB kapatma bildirimi gönderilemedi: "+e.Message);}}))Report("USB kapatma bildirimi kuyruğu dolu; yerel bağlantı kapatıldı.");}
 public void AbortId(int id,string reason,bool notify){Flow flow;lock(Gate){Flows.TryGetValue(id,out flow);Flows.Remove(id);RememberClosed(id);}if(flow!=null)flow.Abort();CloseLink(id);if(notify)NotifyClose(id,reason);}
 public void Finished(Flow flow){lock(Gate){Flow existing;if(Flows.TryGetValue(flow.Id,out existing)&&object.ReferenceEquals(existing,flow))Flows.Remove(flow.Id);RememberClosed(flow.Id);}}
 public void Command(byte[] frame){if(Disposed)return;byte kind=frame[0];try{string exe=Invoke(Bridge,"Find",true) as string;if(exe==null){Reply(kind==43?(byte)44:(byte)46,0,"Client USB paylaşım bileşeni bulunamadı.");return;}if(kind==43){string output=Invoke(Bridge,"Run",exe,"list",false) as string;Reply(44,0,output??"USB listesi alınamadı.");}else{string bus=new UTF8Encoding(false,true).GetString(frame,5,frame.Length-5);if(!Regex.IsMatch(bus,@"^\d{1,4}-\d{1,4}$")){Reply(46,0,"Geçersiz USB bus ID.");return;}string output=Invoke(Bridge,"Run",exe,"bind --busid "+bus,true) as string;Reply(46,0,output??"USB paylaşımı tamamlanamadı.");}}catch(Exception e){Reply(kind==43?(byte)44:(byte)46,0,"USB işlemi başarısız: "+e.Message);}}
 public bool BeginDetach(){return Interlocked.CompareExchange(ref detaching,1,0)==0;}public void EndDetach(){Interlocked.Exchange(ref detaching,0);}
 public void Shutdown(){Flow[] copy;lock(Gate){if(Disposed)return;Disposed=true;copy=new List<Flow>(Flows.Values).ToArray();Flows.Clear();}Controls.Dispose();Notifications.Dispose();foreach(var flow in copy){flow.Abort();CloseLink(flow.Id);}}
 }
 sealed class Flow {
 readonly State owner;public readonly int Id;readonly object gate=new object();readonly Queue<byte[]> queue=new Queue<byte[]>();int bytes;bool closed,started;
 public Flow(State state,int id){owner=state;Id=id;}
 public bool Add(byte[] frame){lock(gate){if(closed)return false;if(queue.Count>=MaxQueuedFrames||frame.Length>MaxQueuedBytes-bytes)return false;queue.Enqueue(frame);bytes+=frame.Length;if(!started){started=true;var thread=new Thread(Run);thread.IsBackground=true;thread.Start();}Monitor.Pulse(gate);return true;}}
 public void Abort(){lock(gate){closed=true;queue.Clear();bytes=0;Monitor.PulseAll(gate);}}
 void Run(){try{while(true){byte[] frame;lock(gate){while(!closed&&!owner.Disposed&&queue.Count==0)Monitor.Wait(gate);if(closed||owner.Disposed)return;frame=queue.Dequeue();bytes-=frame.Length;}Invoke(owner.Bridge,"HandleRaw",frame);if(frame[0]==40&&!owner.HasLink(Id)){owner.AbortId(Id,"USB yerel bağlantısı açılamadı.",true);return;}lock(gate){if(closed||owner.Disposed){owner.CloseLink(Id);return;}}if(frame[0]==42)return;}}catch(Exception e){owner.AbortId(Id,"USB akışı durdu: "+e.Message,true);}finally{owner.Finished(this);}}
 }
 sealed class WorkQueue:IDisposable {
 readonly object gate=new object();readonly Queue<Action> queue=new Queue<Action>();readonly int capacity;bool closed,started;
 public WorkQueue(int max){capacity=max;}
 public bool Add(Action action){lock(gate){if(closed||queue.Count>=capacity)return false;queue.Enqueue(action);if(!started){started=true;var thread=new Thread(Run);thread.IsBackground=true;thread.Start();}Monitor.Pulse(gate);return true;}}
 void Run(){while(true){Action work;lock(gate){while(!closed&&queue.Count==0)Monitor.Wait(gate);if(closed)return;work=queue.Dequeue();}try{work();}catch{}}}
 public void Dispose(){lock(gate){closed=true;queue.Clear();Monitor.PulseAll(gate);}}
 }

public static void DetachOwned(object bridge){throw new InvalidOperationException("Host-only build");}

 



 }
}


