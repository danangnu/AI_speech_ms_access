// IController.cs
using System.Runtime.InteropServices;

namespace AccessEdgeHost
{
    [ComVisible(true)]
    [Guid("5C67F8AB-0C0E-4F7D-9C7E-6A7C0E7A1234")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]   // dual = IUnknown+IDispatch
    public interface IController
    {
        [DispId(1)] void Show(string url);
        [DispId(2)] void Navigate(string url);
        [DispId(3)] void SetHtml(string html);
        [DispId(4)] void Eval(string js);
        [DispId(5)] void Hide();
        [DispId(6)] void Close();
    }
}
