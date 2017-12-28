using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;


namespace TimeFlowShiki
{
    [Serializable]
    public class TackPoint
    {
        public static Action<OnTrackEvent> Emit;

        [SerializeField]
        private TackPointInspector _tackPointInspector;

        [CustomEditor(typeof(TackPointInspector))]
        public class TackPointInspectorGUI : Editor
        {
            public override void OnInspectorGUI()
            {
                var insp = (TackPointInspector)target;

                var tackPoint = insp.TackPoint;
                UpdateTackTitle(tackPoint);


                GUILayout.Space(12);

                var start = tackPoint.Start;
                GUILayout.Label("start:" + start);

                var span = tackPoint.Span;

                var end = start + span - 1;
                GUILayout.Label("end:" + end);

                GUILayout.Label("span:" + span);
            }


            private static void UpdateTackTitle(TackPoint tackPoint)
            {
                var newTitle = EditorGUILayout.TextField("title", tackPoint.Title);
                if (newTitle != tackPoint.Title)
                {
                    tackPoint.BeforeSave();
                    tackPoint.Title = newTitle;
                    tackPoint.Save();
                }
            }
        }

        [SerializeField]
        public string TackId;
        [SerializeField]
        public string ParentTimelineId;
        [SerializeField]
        private int _index;

        [SerializeField]
        private bool _active;
        [SerializeField]
        public bool IsExistTack = true;

        [SerializeField]
        public string Title;
        [SerializeField]
        public int Start;
        [SerializeField]
        public int Span;

        [SerializeField]
        private Texture2D _tackBackTransparentTex;
        [SerializeField]
        private Texture2D _tackColorTex;

        private Vector2 _distance = Vector2.zero;

        private enum TackModifyMode
        {
            None,

            GrabStart,
            GrabBody,
            GrabEnd,
            GrabHalf,

            DragStart,
            DragBody,
            DragEnd
        }
        private TackModifyMode _mode = TackModifyMode.None;

        private Vector2 _dragBeginPoint;

        public TackPoint(int index, string title, int start, int span)
        {
            TackId = TimeFlowShikiGUISettings.ID_HEADER_TACK + Guid.NewGuid();
            _index = index;

            IsExistTack = true;

            Title = title;
            Start = start;
            Span = span;
        }

        public Texture2D GetColorTex()
        {
            return _tackColorTex;
        }

        public void InitializeTackTexture(Texture2D baseTex)
        {
            GenerateTextureFromBaseTexture(baseTex, _index);
        }

        public void SetActive()
        {
            _active = true;

            ApplyDataToInspector();
            Selection.activeObject = _tackPointInspector;
        }

        public void SetDeactive()
        {
            _active = false;
        }

        public bool IsActive()
        {
            return _active;
        }

        public void DrawTack(Rect limitRect, string parentTimelineId, float startX, float startY, bool isUnderEvent)
        {
            if (!IsExistTack) return;

            ParentTimelineId = parentTimelineId;

            var tackBGRect = DrawTackPointInRect(startX, startY);

            var globalMousePos = Event.current.mousePosition;

            var useEvent = false;

            var localMousePos = new Vector2(globalMousePos.x - tackBGRect.x, globalMousePos.y - tackBGRect.y);
            var sizeRect = new Rect(0, 0, tackBGRect.width, tackBGRect.height);

            if (!isUnderEvent) return;

            // mouse event handling.
            switch (_mode)
            {
                case TackModifyMode.None:
                {
                    useEvent = BeginTackModify(tackBGRect, globalMousePos);
                    break;
                }

                case TackModifyMode.GrabStart:
                case TackModifyMode.GrabBody:
                case TackModifyMode.GrabEnd:
                case TackModifyMode.GrabHalf:
                {
                    useEvent = RecognizeTackModify(globalMousePos);
                    break;
                }

                case TackModifyMode.DragStart:
                case TackModifyMode.DragBody:
                case TackModifyMode.DragEnd:
                {
                    useEvent = UpdateTackModify(limitRect, tackBGRect, globalMousePos);
                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException();
            }


            // optional manipulation.
            if (sizeRect.Contains(localMousePos))
            {
                switch (Event.current.type)
                {

                    case EventType.ContextClick:
                    {
                        ShowContextMenu();
                        useEvent = true;
                        break;
                    }

                    // clicked.
                    case EventType.MouseUp:
                    {
                        // right click.
                        if (Event.current.button == 1)
                        {
                            ShowContextMenu();
                            useEvent = true;
                            break;
                        }

                        Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.ObjectSelected, TackId));
                        useEvent = true;
                        break;
                    }
                }
            }

            if (useEvent)
            {
                Event.current.Use();
            }
        }

        public void BeforeSave()
        {
            Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.TackBeforeSave, TackId, Start));
        }

        public void Save()
        {
            Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.TackSave, TackId, Start));
        }


        private void ShowContextMenu()
        {
            var framePoint = Start;
            var menu = new GenericMenu();

            var menuItems = new Dictionary<string, OnTrackEvent.TrackEventType>{
                {"Delete This Tack", OnTrackEvent.TrackEventType.TackDeleted}
            };

            foreach (var key in menuItems.Keys)
            {
                var eventType = menuItems[key];
                menu.AddItem(
                    new GUIContent(key),
                    false,
                    () => Emit(new OnTrackEvent(eventType, this.TackId, framePoint))
                );
            }
            menu.ShowAsContext();
        }

        private void GenerateTextureFromBaseTexture(Texture2D baseTex, int index)
        {
            var samplingColor = baseTex.GetPixels()[0];
            var rgbVector = new Vector3(samplingColor.r, samplingColor.g, samplingColor.b);

            var rotatedVector = Quaternion.AngleAxis(12.5f * index, new Vector3(1.5f * index, 1.25f * index, 1.37f * index)) * rgbVector;

            var slidedColor = new Color(rotatedVector.x, rotatedVector.y, rotatedVector.z, 1);

            _tackBackTransparentTex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            _tackBackTransparentTex.SetPixel(0, 0, new Color(slidedColor.r, slidedColor.g, slidedColor.b, 0.5f));
            _tackBackTransparentTex.Apply();

            _tackColorTex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            _tackColorTex.SetPixel(0, 0, new Color(slidedColor.r, slidedColor.g, slidedColor.b, 1.0f));
            _tackColorTex.Apply();
        }

        private Rect DrawTackPointInRect(float startX, float startY)
        {
            var tackStartPointX = startX + (Start * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH);
            var end = Start + Span - 1;
            var tackEndPointX = startX + (end * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH);

            var tackBGRect = new Rect(tackStartPointX, startY, Span * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH + 1f, TimeFlowShikiGUISettings.TACK_HEIGHT);

            switch (_mode)
            {
                case TackModifyMode.DragStart:
                {
                    tackStartPointX = startX + (Start * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH) + _distance.x;
                    tackBGRect = new Rect(tackStartPointX, startY, Span * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH + 1f - _distance.x, TimeFlowShikiGUISettings.TACK_HEIGHT);
                    break;
                }
                case TackModifyMode.DragBody:
                {
                    tackStartPointX = startX + (Start * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH) + _distance.x;
                    tackEndPointX = startX + (end * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH) + _distance.x;
                    tackBGRect = new Rect(tackStartPointX, startY, Span * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH + 1f, TimeFlowShikiGUISettings.TACK_HEIGHT);
                    break;
                }
                case TackModifyMode.DragEnd:
                {
                    tackEndPointX = startX + (end * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH) + _distance.x;
                    tackBGRect = new Rect(tackStartPointX, startY, Span * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH + _distance.x + 1f, TimeFlowShikiGUISettings.TACK_HEIGHT);
                    break;
                }
            }



            // draw tack.
            {
                // draw bg.
                var frameBGRect = new Rect(tackBGRect.x, tackBGRect.y, tackBGRect.width, TimeFlowShikiGUISettings.TACK_FRAME_HEIGHT);

                GUI.DrawTexture(frameBGRect, _tackBackTransparentTex);

                // draw points.
                {
                    // tackpoint back line.
                    if (Span == 1) GUI.DrawTexture(new Rect(tackBGRect.x + (TimeFlowShikiGUISettings.TACK_FRAME_WIDTH / 3) + 1, startY + (TimeFlowShikiGUISettings.TACK_FRAME_HEIGHT / 3) - 1, (TimeFlowShikiGUISettings.TACK_FRAME_WIDTH / 3) - 1, 11), _tackColorTex);
                    if (1 < Span) GUI.DrawTexture(new Rect(tackBGRect.x + (TimeFlowShikiGUISettings.TACK_FRAME_WIDTH / 2), startY + (TimeFlowShikiGUISettings.TACK_FRAME_HEIGHT / 3) - 1, tackEndPointX - tackBGRect.x, 11), _tackColorTex);

                    // frame start point.
                    DrawTackPoint(Start, tackBGRect.x, startY);

                    // frame end point.
                    if (1 < Span) DrawTackPoint(end, tackEndPointX, startY);
                }

                var routineComponentY = startY + TimeFlowShikiGUISettings.TACK_FRAME_HEIGHT;

                // routine component.
                {
                    var height = TimeFlowShikiGUISettings.ROUTINE_HEIGHT_DEFAULT;
                    if (_active) GUI.DrawTexture(new Rect(tackBGRect.x, routineComponentY, tackBGRect.width, height), TimeFlowShikiGUISettings.ActiveTackBaseTex);

                    GUI.DrawTexture(new Rect(tackBGRect.x + 1, routineComponentY, tackBGRect.width - 2, height - 1), _tackColorTex);

                    GUI.Label(new Rect(tackBGRect.x + 1, routineComponentY, tackBGRect.width - 2, height - 1), Title);
                }
            }

            return tackBGRect;
        }

        private bool BeginTackModify(Rect tackBGRect, Vector2 beginPoint)
        {

            switch (Event.current.type)
            {
                case EventType.MouseDown:
                {
                    var startRect = new Rect(tackBGRect.x, tackBGRect.y, TimeFlowShikiGUISettings.TACK_FRAME_WIDTH, TimeFlowShikiGUISettings.TACK_FRAME_HEIGHT);
                    if (startRect.Contains(beginPoint))
                    {
                        if (Span == 1)
                        {
                            _dragBeginPoint = beginPoint;
                            _mode = TackModifyMode.GrabHalf;
                            return true;
                        }
                        _dragBeginPoint = beginPoint;
                        _mode = TackModifyMode.GrabStart;
                        return true;
                    }
                    var endRect = new Rect(tackBGRect.x + tackBGRect.width - TimeFlowShikiGUISettings.TACK_FRAME_WIDTH, tackBGRect.y, TimeFlowShikiGUISettings.TACK_FRAME_WIDTH, TimeFlowShikiGUISettings.TACK_FRAME_HEIGHT);
                    if (endRect.Contains(beginPoint))
                    {
                        _dragBeginPoint = beginPoint;
                        _mode = TackModifyMode.GrabEnd;
                        return true;
                    }
                    if (tackBGRect.Contains(beginPoint))
                    {
                        _dragBeginPoint = beginPoint;
                        _mode = TackModifyMode.GrabBody;
                        return true;
                    }
                    return false;
                }
            }

            return false;
        }

        private bool RecognizeTackModify(Vector2 mousePos)
        {

            switch (Event.current.type)
            {
                case EventType.MouseDrag:
                {
                    switch (_mode)
                    {
                        case TackModifyMode.GrabStart:
                        {
                            _mode = TackModifyMode.DragStart;
                            Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.ObjectSelected, TackId));
                            return true;
                        }
                        case TackModifyMode.GrabBody:
                        {
                            _mode = TackModifyMode.DragBody;
                            Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.ObjectSelected, TackId));
                            return true;
                        }
                        case TackModifyMode.GrabEnd:
                        {
                            _mode = TackModifyMode.DragEnd;
                            Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.ObjectSelected, TackId));
                            return true;
                        }
                        case TackModifyMode.GrabHalf:
                        {
                            if (mousePos.x < _dragBeginPoint.x) _mode = TackModifyMode.DragStart;
                            if (_dragBeginPoint.x < mousePos.x) _mode = TackModifyMode.DragEnd;
                            Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.ObjectSelected, TackId));
                            return true;
                        }
                    }

                    return false;
                }
                case EventType.MouseUp:
                {
                    _mode = TackModifyMode.None;
                    Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.ObjectSelected, TackId));
                    return true;
                }
            }

            return false;
        }

        private bool UpdateTackModify(Rect limitRect, Rect tackBGRect, Vector2 draggingPoint)
        {
            if (!limitRect.Contains(draggingPoint))
            {
                ExitUpdate(_distance);
                return true;
            }

            // far from bandwidth, exit mode.
            if (draggingPoint.y < 0 || tackBGRect.height + TimeFlowShikiGUISettings.TIMELINE_HEADER_HEIGHT < draggingPoint.y)
            {
                ExitUpdate(_distance);
                return true;
            }

            switch (Event.current.type)
            {
                case EventType.MouseDrag:
                {
                    Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.TackMoving, TackId));

                    _distance = draggingPoint - _dragBeginPoint;
                    var distanceToFrame = DistanceToFrame(_distance.x);

                    switch (_mode)
                    {
                        case TackModifyMode.DragStart:
                        {
                            // limit 0 <= start
                            if ((Start + distanceToFrame) < 0) _distance.x = -FrameToDistance(Start);

                            // limit start <= end
                            if (Span <= (distanceToFrame + 1)) _distance.x = FrameToDistance(Span - 1);
                            break;
                        }
                        case TackModifyMode.DragBody:
                        {
                            // limit 0 <= start
                            if ((Start + distanceToFrame) < 0) _distance.x = -FrameToDistance(Start);
                            break;
                        }
                        case TackModifyMode.DragEnd:
                        {
                            // limit start <= end
                            if ((Span + distanceToFrame) <= 1) _distance.x = -FrameToDistance(Span - 1);
                            break;
                        }
                    }

                    return true;
                }
                case EventType.MouseUp:
                {
                    ExitUpdate(_distance);
                    return true;
                }
            }

            return false;
        }

        private void ExitUpdate(Vector2 currentDistance)
        {
            var distanceToFrame = DistanceToFrame(currentDistance.x);

            Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.TackMoved, TackId));

            switch (_mode)
            {
                case TackModifyMode.DragStart:
                {
                    Start = Start + distanceToFrame;
                    Span = Span - distanceToFrame;
                    break;
                }
                case TackModifyMode.DragBody:
                {
                    Start = Start + distanceToFrame;
                    break;
                }
                case TackModifyMode.DragEnd:
                {
                    Span = Span + distanceToFrame;
                    break;
                }
            }

            if (Start < 0) Start = 0;

            Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.TackMovedAfter, TackId));

            _mode = TackModifyMode.None;

            _distance = Vector2.zero;
        }

        private static int DistanceToFrame(float distX)
        {
            var distanceToFrame = (int)(distX / TimeFlowShikiGUISettings.TACK_FRAME_WIDTH);
            var distanceDelta = distX % TimeFlowShikiGUISettings.TACK_FRAME_WIDTH;

            // adjust behaviour by frame width.
            if (TimeFlowShikiGUISettings.BEHAVE_FRAME_MOVE_RATIO <= distanceDelta) distanceToFrame = distanceToFrame + 1;
            if (distanceDelta <= -TimeFlowShikiGUISettings.BEHAVE_FRAME_MOVE_RATIO) distanceToFrame = distanceToFrame - 1;

            return distanceToFrame;
        }

        private static float FrameToDistance(int frame)
        {
            return TimeFlowShikiGUISettings.TACK_FRAME_WIDTH * frame;
        }

        private void DrawTackPoint(int frame, float pointX, float pointY)
        {
            if (Span == 1)
            {
                if (frame % 5 == 0 && 0 < frame)
                {
                    GUI.DrawTexture(new Rect(pointX + 2, pointY + (TimeFlowShikiGUISettings.TACK_FRAME_HEIGHT / 3) - 2, TimeFlowShikiGUISettings.TACK_POINT_SIZE, TimeFlowShikiGUISettings.TACK_POINT_SIZE), TimeFlowShikiGUISettings.GrayPointSingleTex);
                }
                else
                {
                    GUI.DrawTexture(new Rect(pointX + 2, pointY + (TimeFlowShikiGUISettings.TACK_FRAME_HEIGHT / 3) - 2, TimeFlowShikiGUISettings.TACK_POINT_SIZE, TimeFlowShikiGUISettings.TACK_POINT_SIZE), TimeFlowShikiGUISettings.WhitePointSingleTex);
                }
                return;
            }

            if (frame % 5 == 0 && 0 < frame)
            {
                GUI.DrawTexture(new Rect(pointX + 2, pointY + (TimeFlowShikiGUISettings.TACK_FRAME_HEIGHT / 3) - 2, TimeFlowShikiGUISettings.TACK_POINT_SIZE, TimeFlowShikiGUISettings.TACK_POINT_SIZE), TimeFlowShikiGUISettings.GrayPointTex);
            }
            else
            {
                GUI.DrawTexture(new Rect(pointX + 2, pointY + (TimeFlowShikiGUISettings.TACK_FRAME_HEIGHT / 3) - 2, TimeFlowShikiGUISettings.TACK_POINT_SIZE, TimeFlowShikiGUISettings.TACK_POINT_SIZE), TimeFlowShikiGUISettings.WhitePointTex);
            }
        }

        public void Deleted()
        {
            IsExistTack = false;
        }

        public bool ContainsFrame(int frame)
        {
            return Start <= frame && frame <= Start + Span - 1;
        }

        public void UpdatePos(int start, int span)
        {
            Start = start;
            Span = span;
            ApplyDataToInspector();
        }


        public void ApplyDataToInspector()
        {
            if (_tackPointInspector == null) _tackPointInspector = ScriptableObject.CreateInstance("TackPointInspector") as TackPointInspector;

            if (_tackPointInspector != null) _tackPointInspector.UpdateTackPoint(this);
        }
    }
}