namespace TimeFlowShiki
{
    public class OnTrackEvent
    {
        public enum TrackEventType
        {
            ScoreAddTimeline,

            TimelineAddTack,
            TimelineDelete,
            TimelineBeforeSave,
            TimelineSave,

            TackMoving,
            TackMoved,
            TackMovedAfter,
            TackDeleted,
            TackBeforeSave,
            TackSave,

            ObjectSelected,
            Unselected
        }

        public readonly TrackEventType TrackEvent;
        public readonly string ActiveObjectId;
        public readonly int Frame;

        public OnTrackEvent(TrackEventType trackEvent, string activeObjectId, int frame = -1)
        {
            TrackEvent = trackEvent;
            ActiveObjectId = activeObjectId;
            Frame = frame;
        }

        public OnTrackEvent Copy()
        {
            return new OnTrackEvent(TrackEvent, ActiveObjectId, Frame);
        }
    }
}