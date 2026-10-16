using UnityEngine;
using UnityEngine.EventSystems;

namespace GasStation
{
    public class HoldPressureButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public GameSessionManager session;
        int activePointer = int.MinValue;
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || activePointer != int.MinValue) return;
            activePointer = data.pointerId;
            session.SetPointerHeld(true);
        }
        public void OnPointerUp(PointerEventData data)
        {
            if (data.pointerId == activePointer) Release();
        }
        public void OnPointerExit(PointerEventData data)
        {
            if (data.pointerId == activePointer) Release();
        }
        public void Release() { activePointer = int.MinValue; if (session != null) session.SetPointerHeld(false); }
        void OnDisable() => Release();
    }
}
