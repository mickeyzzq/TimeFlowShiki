using UnityEngine;


namespace TimeFlowShiki
{
    public class TimelineTrackInspector : ScriptableObject
    {
        public TimelineTrack TimelineTrack;

        public void UpdateTimelineTrack(TimelineTrack timelineTrack)
        {
            TimelineTrack = timelineTrack;
        }
    }
}