using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Collections.Generic;


namespace TimeFlowShiki
{
    [Serializable]
    public class TimelineTrack
    {
        public static Action<OnTrackEvent> Emit;

        [SerializeField]
        private TimelineTrackInspector _timelineTrackInspector;

        [CustomEditor(typeof(TimelineTrackInspector))]
        public class TimelineTrackInspectorGUI : Editor
        {
            public override void OnInspectorGUI()
            {
                var insp = (TimelineTrackInspector)target;

                var timelineTrack = insp.TimelineTrack;
                UpdateTimelineTrackTitle(timelineTrack);
            }

            private static void UpdateTimelineTrackTitle(TimelineTrack timelineTrack)
            {
                var newTitle = EditorGUILayout.TextField("title", timelineTrack.Title);
                if (newTitle != timelineTrack.Title)
                {
                    timelineTrack.BeforeSave();
                    timelineTrack.Title = newTitle;
                    timelineTrack.Save();
                }
            }
        }

        [SerializeField]
        private int _index;

        [SerializeField]
        public bool IsExistTimeline;
        [SerializeField]
        public bool Active;
        [SerializeField]
        public string TimelineId;

        [SerializeField]
        public string Title;
        [SerializeField]
        public List<TackPoint> TackPoints = new List<TackPoint>();

        private Rect _trackRect;
        private Texture2D _timelineBaseTexture;

        private float _timelineScrollX;

        private GUIStyle _timelineConditionTypeLabelStyle;
        private GUIStyle _timelineConditionTypeLabelSmallStyle;

        private List<string> _movingTackIds = new List<string>();

        public TimelineTrack()
        {
            InitializeTextResource();


            IsExistTimeline = true;


            // set initial track rect.
            const float DEFAULT_HEIGHT = (TimeFlowShikiGUISettings.TIMELINE_HEIGHT);
            _trackRect = new Rect(0, 0, 10, DEFAULT_HEIGHT);
        }

        public TimelineTrack(int index, string title, IEnumerable<TackPoint> tackPoints)
        {
            InitializeTextResource();

            IsExistTimeline = true;

            TimelineId = TimeFlowShikiGUISettings.ID_HEADER_TIMELINE + Guid.NewGuid();
            _index = index;
            Title = title;
            TackPoints = new List<TackPoint>(tackPoints);

            // set initial track rect.
            var defaultHeight = (TimeFlowShikiGUISettings.TIMELINE_HEIGHT);
            _trackRect = new Rect(0, 0, 10, defaultHeight);

            ApplyTextureToTacks(index);
        }

        private void InitializeTextResource()
        {
            _timelineConditionTypeLabelStyle = new GUIStyle
            {
                normal = { textColor = Color.white },
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };

            _timelineConditionTypeLabelSmallStyle = new GUIStyle
            {
                normal = { textColor = Color.white },
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter
            };
        }

        public void BeforeSave()
        {
            Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.TimelineBeforeSave, TimelineId));
        }

        public void Save()
        {
            Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.TimelineSave, TimelineId));
        }

        /*
			get texture for this timeline, then set texture to every tack.
		*/
        public void ApplyTextureToTacks(int texIndex)
        {
            _timelineBaseTexture = GetTimelineTexture(texIndex);
            foreach (var tackPoint in TackPoints) tackPoint.InitializeTackTexture(_timelineBaseTexture);
        }

        public static Texture2D GetTimelineTexture(int textureIndex)
        {
            var color = TimeFlowShikiGUISettings.ResourceColorsSources[textureIndex % TimeFlowShikiGUISettings.ResourceColorsSources.Count];
            var colorTex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            colorTex.SetPixel(0, 0, color);
            colorTex.Apply();

            return colorTex;
        }

        public float Height()
        {
            return _trackRect.height;
        }

        public int GetIndex()
        {
            return _index;
        }

        public void SetActive()
        {
            Active = true;

            ApplyDataToInspector();
            Selection.activeObject = _timelineTrackInspector;
        }

        public void SetDeactive()
        {
            Active = false;
        }

        public bool IsActive()
        {
            return Active;
        }

        public bool ContainsActiveTack()
        {
            return TackPoints.Any(tackPoint => tackPoint.IsActive());
        }

        public int GetStartFrameById(string objectId)
        {
            foreach (var tackPoint in TackPoints)
            {
                if (tackPoint.TackId == objectId) return tackPoint.Start;
            }
            return -1;
        }

        public void SetTimelineY(float additional)
        {
            _trackRect.y = _trackRect.y + additional;
        }

        public float DrawTimelineTrack(float headWall, float timelineScrollX, float yOffsetPos, float width)
        {
            _timelineScrollX = timelineScrollX;

            _trackRect.width = width;
            _trackRect.y = yOffsetPos;

            if (_trackRect.y < headWall) _trackRect.y = headWall;

            if (_timelineBaseTexture == null) ApplyTextureToTacks(_index);

            _trackRect = GUI.Window(_index, _trackRect, WindowEventCallback, string.Empty, "AnimationKeyframeBackground");
            return _trackRect.height;
        }

        public float GetY()
        {
            return _trackRect.y;
        }

        public float GetHeight()
        {
            return _trackRect.height;
        }

        private void WindowEventCallback(int id)
        {
            // draw bg from header to footer.
            {
                if (Active)
                {
                    var headerBGActiveRect = new Rect(0f, 0f, _trackRect.width, TimeFlowShikiGUISettings.TIMELINE_HEIGHT);
                    GUI.DrawTexture(headerBGActiveRect, TimeFlowShikiGUISettings.ActiveTackBaseTex);

                    var headerBGRect = new Rect(1f, 1f, _trackRect.width - 1f, TimeFlowShikiGUISettings.TIMELINE_HEIGHT - 2f);
                    GUI.DrawTexture(headerBGRect, TimeFlowShikiGUISettings.TimelineHeaderTex);
                }
                else
                {
                    var headerBGRect = new Rect(0f, 0f, _trackRect.width, TimeFlowShikiGUISettings.TIMELINE_HEIGHT);
                    GUI.DrawTexture(headerBGRect, TimeFlowShikiGUISettings.TimelineHeaderTex);
                }
            }

            const float TIMELINE_BODY_Y = TimeFlowShikiGUISettings.TIMELINE_HEADER_HEIGHT;

            // timeline condition type box.	
            var conditionBGRect = new Rect(1f, TIMELINE_BODY_Y, TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_WIDTH - 1f, TimeFlowShikiGUISettings.TACK_HEIGHT - 1f);
            if (Active)
            {
                var conditionBGRectInActive = new Rect(1f, TIMELINE_BODY_Y, TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_WIDTH - 1f, TimeFlowShikiGUISettings.TACK_HEIGHT - 1f);
                GUI.DrawTexture(conditionBGRectInActive, _timelineBaseTexture);
            }
            else
            {
                GUI.DrawTexture(conditionBGRect, _timelineBaseTexture);
            }

            // draw timeline title.
            if (!string.IsNullOrEmpty(Title))
            {
                GUI.Label(
                    new Rect(
                        0f,
                        TimeFlowShikiGUISettings.TIMELINE_HEADER_HEIGHT - 1f,
                        TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_WIDTH,
                        TimeFlowShikiGUISettings.TACK_HEIGHT
                    ),
                    Title,
                    Title.Length < 9 ? _timelineConditionTypeLabelStyle : _timelineConditionTypeLabelSmallStyle
                );
            }


            var frameRegionWidth = _trackRect.width - TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_SPAN;

            // draw frame back texture & TackPoint datas on frame.
            GUI.BeginGroup(new Rect(TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_SPAN, TIMELINE_BODY_Y, _trackRect.width, TimeFlowShikiGUISettings.TACK_HEIGHT));
            {
                DrawFrameRegion(_timelineScrollX, 0f, frameRegionWidth);
            }
            GUI.EndGroup();

            var useEvent = false;

            // mouse manipulation.
            switch (Event.current.type)
            {
                case EventType.ContextClick:
                {
                    ShowContextMenu(_timelineScrollX);
                    useEvent = true;
                    break;
                }

                // clicked.
                case EventType.MouseUp:
                {
                    // is right clicked
                    if (Event.current.button == 1)
                    {
                        ShowContextMenu(_timelineScrollX);
                        useEvent = true;
                        break;
                    }

                    Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.ObjectSelected, TimelineId));
                    useEvent = true;
                    break;
                }
            }

            // constraints.
            _trackRect.x = 0;
            if (_trackRect.y < 0) _trackRect.y = 0;

            GUI.DragWindow();
            if (useEvent) Event.current.Use();
        }

        private void ShowContextMenu(float scrollX)
        {
            var targetFrame = GetFrameOnTimelineFromLocalMousePos(Event.current.mousePosition, scrollX);
            var menu = new GenericMenu();

            var menuItems = new Dictionary<string, OnTrackEvent.TrackEventType>{
                {"Add New Tack", OnTrackEvent.TrackEventType.TimelineAddTack},
                {"Delete This Timeline", OnTrackEvent.TrackEventType.TimelineDelete},

                // not implemented yet.
                // {"Copy This Timeline", OnTrackEvent.EventType.EVENT_TIMELINE_COPY},
                // {"Paste Tack", OnTrackEvent.EventType.EVENT_TACK_PASTE},
                // {"Cut This Timeline", OnTrackEvent.EventType.EVENT_TIMELINE_CUT},
                // {"Hide This Timeline", OnTrackEvent.EventType.EVENT_TIMELINE_HIDE},
            };

            foreach (var key in menuItems.Keys)
            {
                var eventType = menuItems[key];
                var enable = IsEnableEvent(eventType, targetFrame);
                if (enable)
                {
                    menu.AddItem(
                        new GUIContent(key),
                        false,
                        () => Emit(new OnTrackEvent(eventType, TimelineId, targetFrame))
                    );
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent(key));
                }
            }
            menu.ShowAsContext();
        }

        private bool IsEnableEvent(OnTrackEvent.TrackEventType eventType, int frame)
        {
            switch (eventType)
            {
                case OnTrackEvent.TrackEventType.TimelineAddTack:
                {
                    foreach (var tackPoint in TackPoints)
                    {
                        if (tackPoint.ContainsFrame(frame))
                        {
                            return !tackPoint.IsExistTack;
                        }
                    }
                    return true;
                }
                case OnTrackEvent.TrackEventType.TimelineDelete:
                {
                    return true;
                }


                default:
                {
                    // Debug.LogError("unhandled eventType IsEnableEvent:" + eventType);
                    return false;
                }
            }
        }

        private static int GetFrameOnTimelineFromLocalMousePos(Vector2 localMousePos, float scrollX)
        {
            var frameSourceX = localMousePos.x + Math.Abs(scrollX) - TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_SPAN;
            return GetFrameOnTimelineFromAbsolutePosX(frameSourceX);
        }

        public static int GetFrameOnTimelineFromAbsolutePosX(float frameSourceX)
        {
            return (int)(frameSourceX / TimeFlowShikiGUISettings.TACK_FRAME_WIDTH);
        }

        private void DrawFrameRegion(float timelineScrollX, float timelineBodyY, float frameRegionWidth)
        {
            var limitRect = new Rect(0, 0, frameRegionWidth, TimeFlowShikiGUISettings.TACK_HEIGHT);

            // draw frame background.
            {
                DrawFrameBG(timelineScrollX, timelineBodyY, frameRegionWidth, TimeFlowShikiGUISettings.TACK_FRAME_HEIGHT, false);
            }

            // draw tack points & label on this track in range.
            {
                foreach (var tackPoint in TackPoints)
                {
                    var isUnderEvent = _movingTackIds.Contains(tackPoint.TackId) || !_movingTackIds.Any();

                    // draw tackPoint on the frame.
                    tackPoint.DrawTack(limitRect, TimelineId, timelineScrollX, timelineBodyY, isUnderEvent);
                }
            }
        }

        public static void DrawFrameBG(float timelineScrollX, float timelineBodyY, float frameRegionWidth, float frameRegionHeight, bool showFrameCount)
        {
            var yOffset = timelineBodyY;

            // show 0 count.
            if (showFrameCount)
            {
                if (0 < TimeFlowShikiGUISettings.TACK_FRAME_WIDTH + timelineScrollX) GUI.Label(new Rect(timelineScrollX + 3, 0, 20, TimeFlowShikiGUISettings.CONDITION_INSPECTOR_FRAMECOUNT_HEIGHT), "0");
                yOffset = yOffset + TimeFlowShikiGUISettings.CONDITION_INSPECTOR_FRAMECOUNT_HEIGHT;
            }

            // draw 1st 1 frame.
            if (0 < TimeFlowShikiGUISettings.TACK_FRAME_WIDTH + timelineScrollX)
            {
                GUI.DrawTexture(new Rect(timelineScrollX, yOffset, TimeFlowShikiGUISettings.TACK_5FRAME_WIDTH, frameRegionHeight), TimeFlowShikiGUISettings.FrameTex);
            }


            var repeatCount = (frameRegionWidth - timelineScrollX) / TimeFlowShikiGUISettings.TACK_5FRAME_WIDTH;
            for (var i = 0; i < repeatCount; i++)
            {
                var xPos = TimeFlowShikiGUISettings.TACK_FRAME_WIDTH + timelineScrollX + (i * TimeFlowShikiGUISettings.TACK_5FRAME_WIDTH);
                if (xPos + TimeFlowShikiGUISettings.TACK_5FRAME_WIDTH < 0) continue;

                if (showFrameCount)
                {
                    var frameCountStr = ((i + 1) * 5).ToString();
                    var span = 0;
                    if (2 < frameCountStr.Length) span = ((frameCountStr.Length - 2) * 8) / 2;
                    GUI.Label(new Rect(xPos + (TimeFlowShikiGUISettings.TACK_FRAME_WIDTH * 4) - span, 0, frameCountStr.Length * 10, TimeFlowShikiGUISettings.CONDITION_INSPECTOR_FRAMECOUNT_HEIGHT), frameCountStr);
                }
                var frameRect = new Rect(xPos, yOffset, TimeFlowShikiGUISettings.TACK_5FRAME_WIDTH, frameRegionHeight);
                GUI.DrawTexture(frameRect, TimeFlowShikiGUISettings.FrameTex);
            }
        }

        public void SelectPreviousTackOf(string tackId)
        {
            var cursoredTackIndex = TackPoints.FindIndex(tack => tack.TackId == tackId);

            if (cursoredTackIndex == 0)
            {
                Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.ObjectSelected, TimelineId));
                return;
            }

            var currentExistTacks = TackPoints.Where(tack => tack.IsExistTack).OrderByDescending(tack => tack.Start).ToList();
            var currentTackIndex = currentExistTacks.FindIndex(tack => tack.TackId == tackId);

            if (0 <= currentTackIndex && currentTackIndex < currentExistTacks.Count - 1)
            {
                var nextTack = currentExistTacks[currentTackIndex + 1];
                Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.ObjectSelected, nextTack.TackId));
            }
        }

        public void SelectNextTackOf(string tackId)
        {
            var currentExistTacks = TackPoints.Where(tack => tack.IsExistTack).OrderBy(tack => tack.Start).ToList();
            var currentTackIndex = currentExistTacks.FindIndex(tack => tack.TackId == tackId);

            if (0 <= currentTackIndex && currentTackIndex < currentExistTacks.Count - 1)
            {
                var nextTack = currentExistTacks[currentTackIndex + 1];
                Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.ObjectSelected, nextTack.TackId));
            }
        }

        public void SelectDefaultTackOrSelectTimeline()
        {
            if (TackPoints.Any())
            {
                var firstTackPoint = TackPoints[0];
                Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.ObjectSelected, firstTackPoint.TackId));
                return;
            }

            Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.ObjectSelected, TimelineId));
        }

        public void ActivateTacks(List<string> activeTackIds)
        {
            foreach (var tackPoint in TackPoints)
            {
                if (activeTackIds.Contains(tackPoint.TackId))
                {
                    tackPoint.SetActive();
                }
                else
                {
                    tackPoint.SetDeactive();
                }
            }
        }

        public void DeactivateTacks()
        {
            foreach (var tackPoint in TackPoints)
            {
                tackPoint.SetDeactive();
            }
        }

        public List<TackPoint> TacksByIds(List<string> tackIds)
        {
            return TackPoints.Where(tackPoint => tackIds.Contains(tackPoint.TackId)).ToList();
        }

        /**
			returns the tack which has nearlest start point.
		*/
        public List<TackPoint> TacksByStart(int startPos)
        {
            var startIndex = TackPoints.FindIndex(tack => startPos <= tack.Start);
            if (0 <= startIndex)
            {
                // if index - 1 tack contains startPos, return it.
                if (0 < startIndex && (startPos <= TackPoints[startIndex - 1].Start + TackPoints[startIndex - 1].Span - 1))
                {
                    return new List<TackPoint> { TackPoints[startIndex - 1] };
                }
                return new List<TackPoint> { TackPoints[startIndex] };
            }

            // no candidate found in area, but if any tack exists, select the last of it. 
            return TackPoints.Any() ? new List<TackPoint> { TackPoints[TackPoints.Count - 1] } : new List<TackPoint>();
        }

        public bool ContainsTackById(string tackId)
        {
            return TackPoints.Any(tackPoint => tackId == tackPoint.TackId);
        }

        public void Deleted()
        {
            IsExistTimeline = false;
        }

        public void UpdateByTackMoved(string tackId)
        {
            _movingTackIds.Clear();

            var movedTack = TacksByIds(new List<string> { tackId })[0];

            movedTack.ApplyDataToInspector();

            foreach (var targetTack in TackPoints)
            {
                if (targetTack.TackId == tackId) continue;
                if (!targetTack.IsExistTack) continue;

                // not contained case.
                if (targetTack.Start + (targetTack.Span - 1) < movedTack.Start) continue;
                if (movedTack.Start + (movedTack.Span - 1) < targetTack.Start) continue;

                // movedTack contained targetTack, delete.
                if (movedTack.Start <= targetTack.Start && targetTack.Start + (targetTack.Span - 1) <= movedTack.Start + (movedTack.Span - 1))
                {
                    Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.TackDeleted, targetTack.TackId));
                    continue;
                }

                // moved tack's tail is contained by target tack, update.
                // m-tm-t
                if (movedTack.Start <= targetTack.Start + (targetTack.Span - 1) && targetTack.Start + (targetTack.Span - 1) <= movedTack.Start + (movedTack.Span - 1))
                {
                    var resizedSpan = movedTack.Start - targetTack.Start;
                    targetTack.UpdatePos(targetTack.Start, resizedSpan);
                    continue;
                }

                // moved tack's head is contained by target tack's tail, update.
                // t-mt-m
                if (targetTack.Start <= movedTack.Start + (movedTack.Span - 1) && movedTack.Start <= targetTack.Start)
                {
                    var newStartPos = movedTack.Start + movedTack.Span;
                    var resizedSpan = targetTack.Span - (newStartPos - targetTack.Start);
                    targetTack.UpdatePos(newStartPos, resizedSpan);
                    continue;
                }

                if (targetTack.Start < movedTack.Start && movedTack.Start + movedTack.Span < targetTack.Start + targetTack.Span)
                {
                    var resizedSpanPoint = movedTack.Start - 1;
                    var resizedSpan = resizedSpanPoint - targetTack.Start + 1;
                    targetTack.UpdatePos(targetTack.Start, resizedSpan);
                }
            }
        }

        public void SetMovingTack(string tackId)
        {
            _movingTackIds = new List<string> { tackId };
        }

        public void AddNewTackToEmptyFrame(int frame)
        {
            var newTackPoint = new TackPoint(
                TackPoints.Count,
                TimeFlowShikiGUISettings.DEFAULT_TACK_NAME,
                frame,
                TimeFlowShikiGUISettings.DEFAULT_TACK_SPAN
            );
            TackPoints.Add(newTackPoint);

            ApplyTextureToTacks(_index);
        }

        public void DeleteTackById(string tackId)
        {
            var deletedTackIndex = TackPoints.FindIndex(tack => tack.TackId == tackId);
            if (deletedTackIndex == -1) return;
            TackPoints[deletedTackIndex].Deleted();
        }

        public void ApplyDataToInspector()
        {
            if (_timelineTrackInspector == null) _timelineTrackInspector = ScriptableObject.CreateInstance("TimelineTrackInspector") as TimelineTrackInspector;

            if (_timelineTrackInspector != null) _timelineTrackInspector.UpdateTimelineTrack(this);

            foreach (var tackPoint in TackPoints)
            {
                tackPoint.ApplyDataToInspector();
            }
        }
    }
}