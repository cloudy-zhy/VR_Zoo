using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class WorldUIMouseClick : MonoBehaviour
{
    private EventSystem evtSys;
    void Start()
    {
        evtSys = EventSystem.current;
    }
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            PointerEventData data = new PointerEventData(evtSys);
            data.position = Input.mousePosition;
            List<RaycastResult> list = new List<RaycastResult>();
            evtSys.RaycastAll(data, list);
            if (list.Count > 0)
            {
                ExecuteEvents.Execute(list[0].gameObject, data, ExecuteEvents.pointerClickHandler);
            }
        }
    }
}