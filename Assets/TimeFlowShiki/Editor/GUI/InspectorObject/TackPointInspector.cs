using UnityEngine;


namespace TimeFlowShiki
{
    public class TackPointInspector : ScriptableObject
    {
        public TackPoint TackPoint;

        public void UpdateTackPoint(TackPoint tackPoint)
        {
            TackPoint = tackPoint;
        }
    }
}