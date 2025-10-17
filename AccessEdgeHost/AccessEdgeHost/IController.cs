using System.Runtime.InteropServices;

namespace AccessEdgeHost
{
    [ComVisible(true)]
    [Guid("8F75B6D6-5D4B-4E9F-B2B7-9E4C5F2EAB10")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IController
    {
        [DispId(1)]  void Show(string url);
        [DispId(2)]  void Navigate(string url);
        [DispId(3)]  void SetHtml(string html);
        [DispId(4)]  void Eval(string js);
        [DispId(5)]  void Hide();
        [DispId(6)]  void Close();

        // 👉 add these so VBA can call them
        [DispId(7)]  void SetCallback([MarshalAs(UnmanagedType.IDispatch)] object cb);
        [DispId(8)]  void PostMessage(string text);
        [DispId(9)]  void ShowDevTools();
        [DispId(10)] void NavigateFile(string fullPath);
    }
}
