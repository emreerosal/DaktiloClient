using System;using System.ComponentModel;using System.Runtime.InteropServices;using System.Threading;
namespace MrDaktilo.Startup {
 // Execution requests belong to this thread. Acquire/release on the GUI thread.
 public sealed class KeepAwake:IDisposable {
  const uint Continuous=0x80000000,SystemRequired=1,DisplayRequired=2;
  [DllImport("kernel32.dll",SetLastError=true)]static extern uint SetThreadExecutionState(uint flags);
  readonly Func<uint,uint> request;readonly int thread;uint previous;bool released;
  public KeepAwake():this(SetThreadExecutionState){}
  KeepAwake(Func<uint,uint> native){request=native;thread=Thread.CurrentThread.ManagedThreadId;previous=request(Continuous|SystemRequired|DisplayRequired);if(previous==0)throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows uyku engelleme isteğini kabul etmedi.");}
  public void Dispose(){if(released)return;if(Thread.CurrentThread.ManagedThreadId!=thread)throw new InvalidOperationException("Power request must be released on its acquiring thread");if(request(previous|Continuous)==0)throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows güç isteği kaldırılamadı.");released=true;}
  public static void Run(System.Windows.Forms.Form form){KeepAwake lease=null;try{try{lease=new KeepAwake();}catch(Exception error){form.Shown+=(s,e)=>System.Windows.Forms.MessageBox.Show(form,error.Message,"Uyku engelleme etkin değil",System.Windows.Forms.MessageBoxButtons.OK,System.Windows.Forms.MessageBoxIcon.Warning);}System.Windows.Forms.Application.Run(form);}finally{if(lease!=null)try{lease.Dispose();}catch(Exception error){System.Diagnostics.Trace.TraceError(error.ToString());}}}
  public static void Test(){uint set=0;int calls=0;using(var lease=new KeepAwake(flags=>{set=flags;calls++;return Continuous;})){if(set!=(Continuous|SystemRequired|DisplayRequired))throw new Exception("Power flags incorrect");}if(calls!=2||set!=Continuous)throw new Exception("Power lease not restored");bool rejected=false;try{new KeepAwake(flags=>0);}catch(Win32Exception){rejected=true;}if(!rejected)throw new Exception("Native failure hidden");using(var lease=new KeepAwake()){} }
 }
}
