using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Collections.Generic;


namespace TimeFlowShiki
{
    [Serializable]
    public class ScoreComponent
    {
        public static Action<OnTrackEvent> Emit;

        [SerializeField]
        private ScoreComponentInspector _scoreComponentInspector;

        [CustomEditor(typeof(ScoreComponentInspector))]
        public class ScoreComponentInspectorGUI : Editor
        {
            public override void OnInspectorGUI()
            {
                var insp = (ScoreComponentInspector)target;

                var title = insp.Title;
                GUILayout.Label("title:" + title);
            }
        }

        [SerializeField]
        public bool IsExistScore;
        [SerializeField]
        private bool _active;
        [SerializeField]
        public List<TimelineTrack> TimelineTracks;

        // this id is for idenitify at editing.
        [SerializeField]
        public string ScoreId;

        // this id is for name of this auto.
        [SerializeField]
        public string Id;
        [SerializeField]
        public string Title;


        public ScoreComponent() { }


        public ScoreComponent(string id, string title, IEnumerable<TimelineTrack> timelineTracks)
        {
            IsExistScore = true;
            _active = false;

            ScoreId = TimeFlowShikiGUISettings.ID_HEADER_SCORE + Guid.NewGuid();

            Id = id;
            Title = title;

            TimelineTracks = new List<TimelineTrack>(timelineTracks);
        }


        public bool IsActive()
        {
            return _active;
        }

        public void SetActive()
        {
            _active = true;

            ApplyDataToInspector();
        }

        public void ShowInspector()
        {
            Debug.LogError("autoのinspectorをセットする。");
        }

        public void SetDeactive()
        {
            _active = false;
        }


        public void DrawTimelines(ScoreComponent auto, float yOffsetPos, float xScrollIndex, float trackWidth)
        {
            var yIndex = yOffsetPos;

            foreach (var timelineTrack in TimelineTracks)
            {
                if (!timelineTrack.IsExistTimeline) continue;

                var trackHeight = timelineTrack.DrawTimelineTrack(yOffsetPos, xScrollIndex, yIndex, trackWidth);

                // set next y index.
                yIndex = yIndex + trackHeight + TimeFlowShikiGUISettings.TIMELINE_SPAN;
            }
        }

        public float TimelinesTotalHeight()
        {
            return TimelineTracks.Sum(timelineTrack => timelineTrack.Height());
        }

        public List<TimelineTrack> TimelinesByIds(List<string> timelineIds)
        {
            return TimelineTracks.Where(timelineTrack => timelineIds.Contains(timelineTrack.TimelineId)).ToList();
        }

        public TackPoint TackById(string tackId)
        {
            return TimelineTracks.SelectMany(timelineTrack => timelineTrack.TackPoints).FirstOrDefault(tack => tack.TackId == tackId);
        }

        public void SelectAboveObjectById(string currentActiveObjectId)
        {
            if (TimeFlowShikiGUIWindow.IsTimelineId(currentActiveObjectId))
            {
                var candidateTimelines = TimelineTracks.Where(timeline => timeline.IsExistTimeline).OrderBy(timeline => timeline.GetIndex()).ToList();
                var currentTimelineIndex = candidateTimelines.FindIndex(timeline => timeline.TimelineId == currentActiveObjectId);

                if (0 < currentTimelineIndex)
                {
                    var targetTimeline = TimelineTracks[currentTimelineIndex - 1];
                    Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.ObjectSelected, targetTimeline.TimelineId));
                    return;
                }

                return;
            }

            if (TimeFlowShikiGUIWindow.IsTackId(currentActiveObjectId))
            {
                /*
					select another timeline's same position tack.
				*/
                var currentActiveTack = TackById(currentActiveObjectId);

                var currentActiveTackStart = currentActiveTack.Start;
                var currentTimelineId = currentActiveTack.ParentTimelineId;

                var aboveTimeline = AboveTimeline(currentTimelineId);
                if (aboveTimeline != null)
                {
                    var nextActiveTacks = aboveTimeline.TacksByStart(currentActiveTackStart);
                    if (nextActiveTacks.Any())
                    {
                        var targetTack = nextActiveTacks[0];
                        Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.ObjectSelected, targetTack.TackId));
                    }
                    else
                    {
                        // no tack found, select timeline itself.
                        Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.ObjectSelected, aboveTimeline.TimelineId));
                    }
                }
            }
        }

        public void SelectBelowObjectById(string currentActiveObjectId)
        {
            if (TimeFlowShikiGUIWindow.IsTimelineId(currentActiveObjectId))
            {
                var cursoredTimelineIndex = TimelineTracks.FindIndex(timeline => timeline.TimelineId == currentActiveObjectId);
                if (cursoredTimelineIndex < TimelineTracks.Count - 1)
                {
                    var targetTimelineIndex = cursoredTimelineIndex + 1;
                    var targetTimeline = TimelineTracks[targetTimelineIndex];
                    Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.ObjectSelected, targetTimeline.TimelineId));
                }
                return;
            }

            if (TimeFlowShikiGUIWindow.IsTackId(currentActiveObjectId))
            {
                /*
					select another timeline's same position tack.
				*/
                var currentActiveTack = TackById(currentActiveObjectId);

                var currentActiveTackStart = currentActiveTack.Start;
                var currentTimelineId = currentActiveTack.ParentTimelineId;

                var belowTimeline = BelowTimeline(currentTimelineId);
                if (belowTimeline != null)
                {
                    var nextActiveTacks = belowTimeline.TacksByStart(currentActiveTackStart);
                    if (nextActiveTacks.Any())
                    {
                        var targetTack = nextActiveTacks[0];
                        Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.ObjectSelected, targetTack.TackId));
                    }
                    else
                    {
                        // no tack found, select timeline itself.
                        Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.ObjectSelected, belowTimeline.TimelineId));
                    }
                }
            }
        }

        private TimelineTrack AboveTimeline(string baseTimelineId)
        {
            var baseIndex = TimelineTracks.FindIndex(timeline => timeline.TimelineId == baseTimelineId);
            return 0 < baseIndex ? TimelineTracks[baseIndex - 1] : null;
        }

        private TimelineTrack BelowTimeline(string baseTimelineId)
        {
            var baseIndex = TimelineTracks.FindIndex(timeline => timeline.TimelineId == baseTimelineId);
            return baseIndex < TimelineTracks.Count - 1 ? TimelineTracks[baseIndex + 1] : null;
        }

        public void SelectPreviousTackOfTimelines(string currentActiveObjectId)
        {
            /*
				if current active id is tack, select previous one.
				and if active tack is the head of timeline, select timeline itself.
			*/
            if (TimeFlowShikiGUIWindow.IsTackId(currentActiveObjectId))
            {
                foreach (var timelineTrack in TimelineTracks)
                {
                    timelineTrack.SelectPreviousTackOf(currentActiveObjectId);
                }
            }
        }

        public void SelectNextTackOfTimelines(string currentActiveObjectId)
        {
            // if current active id is timeline, select first tack of that.
            if (TimeFlowShikiGUIWindow.IsTimelineId(currentActiveObjectId))
            {
                foreach (var timelineTrack in TimelineTracks)
                {
                    if (timelineTrack.TimelineId == currentActiveObjectId)
                    {
                        timelineTrack.SelectDefaultTackOrSelectTimeline();
                    }
                }
                return;
            }

            // if current active id is tack, select next one.
            if (TimeFlowShikiGUIWindow.IsTackId(currentActiveObjectId))
            {
                foreach (var timelineTrack in TimelineTracks)
                {
                    timelineTrack.SelectNextTackOf(currentActiveObjectId);
                }
            }
        }

        public bool IsActiveTimelineOrContainsActiveObject(int index)
        {
            if (index < TimelineTracks.Count)
            {
                var currentTimeline = TimelineTracks[index];
                return currentTimeline.IsActive() || currentTimeline.ContainsActiveTack();
            }
            return false;
        }

        public int GetStartFrameById(string objectId)
        {
            if (TimeFlowShikiGUIWindow.IsTimelineId(objectId))
            {
                return -1;
            }

            if (TimeFlowShikiGUIWindow.IsTackId(objectId))
            {
                var targetContainedTimelineIndex = GetTackContainedTimelineIndex(objectId);
                if (0 <= targetContainedTimelineIndex)
                {
                    var foundStartFrame = TimelineTracks[targetContainedTimelineIndex].GetStartFrameById(objectId);
                    if (0 <= foundStartFrame) return foundStartFrame;
                }
            }

            return -1;
        }

        public void SelectTackAtFrame(int frameCount)
        {
            if (TimelineTracks.Any())
            {
                var firstTimelineTrack = TimelineTracks[0];
                var nextActiveTacks = firstTimelineTrack.TacksByStart(frameCount);
                if (nextActiveTacks.Any())
                {
                    var targetTack = nextActiveTacks[0];
                    Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.ObjectSelected, targetTack.TackId));
                }
                else
                {
                    // no tack found, select timeline itself.
                    Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.ObjectSelected, firstTimelineTrack.TimelineId));
                }
            }
        }

        public void DeactivateAllObjects()
        {
            foreach (var timelineTrack in TimelineTracks)
            {
                timelineTrack.SetDeactive();
                timelineTrack.DeactivateTacks();
            }
        }

        public void SetMovingTackToTimelimes(string tackId)
        {
            foreach (var timelineTrack in TimelineTracks)
            {
                if (timelineTrack.ContainsTackById(tackId))
                {
                    timelineTrack.SetMovingTack(tackId);
                }
            }
        }

        /**
			set active to active objects, and set deactive to all other objects.
			affect to records of Undo/Redo.
		*/
        public void ActivateObjectsAndDeactivateOthers(List<string> activeObjectIds)
        {
            foreach (var timelineTrack in TimelineTracks)
            {
                if (activeObjectIds.Contains(timelineTrack.TimelineId)) timelineTrack.SetActive();
                else timelineTrack.SetDeactive();

                timelineTrack.ActivateTacks(activeObjectIds);
            }
        }

        public int GetTackContainedTimelineIndex(string tackId)
        {
            return TimelineTracks.FindIndex(timelineTrack => timelineTrack.ContainsTackById(tackId));
        }

        public void AddNewTackToTimeline(string timelineId, int frame)
        {
            var targetTimeline = TimelinesByIds(new List<string> { timelineId })[0];
            targetTimeline.AddNewTackToEmptyFrame(frame);
        }

        public void DeleteObjectById(string deletedObjectId)
        {
            foreach (var timelineTrack in TimelineTracks)
            {
                if (TimeFlowShikiGUIWindow.IsTimelineId(deletedObjectId))
                {
                    if (timelineTrack.TimelineId == deletedObjectId)
                    {
                        timelineTrack.Deleted();
                    }
                }
                if (TimeFlowShikiGUIWindow.IsTackId(deletedObjectId))
                {
                    timelineTrack.DeleteTackById(deletedObjectId);
                }
            }
        }

        public bool HasAnyValidTimeline()
        {
            return TimelineTracks.Any();
        }

        public int GetIndexOfTimelineById(string timelineId)
        {
            return TimelineTracks.FindIndex(timeline => timeline.TimelineId == timelineId);
        }

        public void ApplyDataToInspector()
        {
            if (_scoreComponentInspector == null) _scoreComponentInspector = ScriptableObject.CreateInstance("ScoreComponentInspector") as ScoreComponentInspector;

            if (_scoreComponentInspector != null) _scoreComponentInspector.Title = Title;
        }
    }
}