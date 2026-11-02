using System;using System.Reflection;using System.Runtime.CompilerServices;using System.Windows.Forms;
namespace MrDaktilo.Files {
 public static class FileFeature {
  sealed class State {public FileSession Session;}
  sealed class Lifetime {public readonly object Gate=new object();public bool Closed;public State State;}
  static readonly ConditionalWeakTable<object,Lifetime> lifetimes=new ConditionalWeakTable<object,Lifetime>();
  static WeakReference clientForm;
  static State Get(object bridge){var lifetime=lifetimes.GetValue(bridge,b=>new Lifetime());lock(lifetime.Gate){if(lifetime.Closed)return null;if(lifetime.State!=null)return lifetime.State;
   var channel=bridge.GetType().GetField("channel",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(bridge);
   var send=channel.GetType().GetMethod("Send");var state=new State();state.Session=new FileSession(true,bytes=>{try{send.Invoke(channel,new object[]{bytes});}catch(TargetInvocationException error){throw error.InnerException??error;}});
   state.Session.Status+=text=>{var form=clientForm==null?null:clientForm.Target as Form;if(form==null)return;Post(form,()=>{var field=form.GetType().GetField("footerText",BindingFlags.Instance|BindingFlags.NonPublic);var label=field==null?null:field.GetValue(form) as Label;if(label!=null)label.Text=text;});};
   lifetime.State=state;return state;}}
  public static bool Intercept(object bridge,byte[] packet){if(packet==null||packet.Length==0||packet[0]<60||packet[0]>69)return false;var state=Get(bridge);if(state!=null)state.Session.Handle(packet);return true;}
  public static void BuildUi(Form form){clientForm=new WeakReference(form);}
  static State Close(object bridge){var lifetime=lifetimes.GetValue(bridge,b=>new Lifetime());lock(lifetime.Gate){lifetime.Closed=true;return lifetime.State;}}
  public static void AbortFast(object bridge){var state=Close(bridge);if(state!=null)state.Session.RequestAbort();}
  public static void Disconnected(object bridge){var state=Close(bridge);if(state!=null)state.Session.Dispose();}
  static void Post(Control control,Action action){try{if(!control.IsDisposed&&control.IsHandleCreated)control.BeginInvoke(action);}catch(InvalidOperationException){}}
 }
}
