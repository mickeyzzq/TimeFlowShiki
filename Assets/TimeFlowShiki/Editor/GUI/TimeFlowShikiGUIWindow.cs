using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using MiniJSONForTimeFlowShiki;


namespace TimeFlowShiki
{
    public class TimeFlowShikiGUIWindow : EditorWindow
    {
        [SerializeField]
        private List<ScoreComponent> _scores = new List<ScoreComponent>();

        private DateTime _lastLoaded = DateTime.MinValue;

        private struct ManipulateTargets
        {
            public readonly List<string> ActiveObjectIds;

            public ManipulateTargets(List<string> activeObjectIds)
            {
                ActiveObjectIds = activeObjectIds;
            }
        }
        private ManipulateTargets _manipulateTargets = new ManipulateTargets(new List<string>());

        private float _selectedPos;
        private int _selectedFrame;
        private float _cursorPos;
        private float _scrollPos;
        private bool _repaint;

        private GUIStyle _activeFrameLabelStyle;
        private GUIStyle _activeConditionValueLabelStyle;

        private struct ManipulateEvents
        {
            public bool KeyLeft;
            public bool KeyRight;
            public bool KeyUp;
            public bool KeyDown;
        }
        private ManipulateEvents _manipulateEvents;

        private readonly List<OnTrackEvent> _eventStacks = new List<OnTrackEvent>();

        /**
			Menu item for AssetGraph.
		*/
        [MenuItem("Window/TimeFlowShiki")]
        private static void ShowEditor()
        {
            GetWindow<TimeFlowShikiGUIWindow>();
        }

        public void OnEnable()
        {
            InitializeResources();

            // handler for Undo/Redo
            Undo.undoRedoPerformed += () => {
                SaveData();
                Repaint();
            };

            ScoreComponent.Emit = Emit;
            TimelineTrack.Emit = Emit;
            TackPoint.Emit = Emit;


            InitializeScoreView();
        }

        private void InitializeScoreView()
        {
            titleContent = new GUIContent("TimelineKit");

            wantsMouseMove = true;
            minSize = new Vector2(600f, 300f);

            _scrollPos = 0;

            ReloadSavedData();
        }

        private void ReloadSavedData()
        {
            /*
				load saved data.
			*/
            var dataPath = Path.Combine(Application.dataPath, TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_FILEPATH);
            var deserialized = new Dictionary<string, object>();
            var lastModified = DateTime.Now;

            if (File.Exists(dataPath))
            {
                // load
                deserialized = LoadData(dataPath);

                var lastModifiedStr = deserialized[TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_LASTMODIFIED] as string;
                lastModified = Convert.ToDateTime(lastModifiedStr);
            }

            /*
				do nothing if json does not modified after load.
			*/
            if (lastModified == _lastLoaded) return;
            _lastLoaded = lastModified;

            if (deserialized.Any()) _scores = LoadScores(deserialized);

            // load demo data then save it.
            if (!_scores.Any())
            {
                var firstAuto = GenerateFirstScore();
                _scores.Add(firstAuto);

                SaveData();
            }


            SetActiveScore(0);
        }

        private static List<ScoreComponent> LoadScores(IDictionary<string, object> deserialized)
        {
            var newScores = new List<ScoreComponent>();

            var scoresList = deserialized[TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_SCORES] as List<object>;

            if (scoresList != null)
                foreach (var score in scoresList)
                {
                    var scoreDict = score as Dictionary<string, object>;
                    var scoreId = scoreDict[TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_SCORE_ID] as string;
                    var scoreTitle = scoreDict[TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_SCORE_TITLE] as string;
                    var scoreTimelines =
                        scoreDict[TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_SCORE_TIMELINES] as List<object>;

                    var currentTimelines = new List<TimelineTrack>();
                    foreach (var scoreTimeline in scoreTimelines)
                    {
                        var scoreTimelineDict = scoreTimeline as Dictionary<string, object>;

                        var timelineTitle =
                            scoreTimelineDict[TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_TIMELINE_TITLE] as string;
                        var timelineTacks =
                            scoreTimelineDict[TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_TIMELINE_TACKS] as List<object>;

                        var currentTacks = new List<TackPoint>();
                        foreach (var timelineTack in timelineTacks)
                        {
                            var timelineTacksDict = timelineTack as Dictionary<string, object>;

                            var tackTitle =
                                timelineTacksDict[TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_TACK_TITLE] as string;
                            var tackStart =
                                Convert.ToInt32(timelineTacksDict[TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_TACK_START]);
                            var tackSpan =
                                Convert.ToInt32(timelineTacksDict[TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_TACK_SPAN]);

                            var newTack = new TackPoint(currentTacks.Count, tackTitle, tackStart, tackSpan);

                            currentTacks.Add(newTack);
                        }

                        var newTimeline = new TimelineTrack(currentTimelines.Count, timelineTitle, currentTacks);
                        currentTimelines.Add(newTimeline);
                    }
                    var newScore = new ScoreComponent(scoreId, scoreTitle, currentTimelines);
                    newScores.Add(newScore);
                }
            return newScores;
        }

        private static Dictionary<string, object> LoadData(string dataPath)
        {
            string dataStr;

            using (var sr = new StreamReader(dataPath))
            {
                dataStr = sr.ReadToEnd();
            }
            return Json.Deserialize(dataStr) as Dictionary<string, object>;
        }

        /**
			convert score - timeline - tack datas to data tree.
		*/
        private void SaveData()
        {
            var lastModified = DateTime.Now;
            var currentScores = _scores;
            var currentScoreList = new List<object>();

            foreach (var score in currentScores)
            {
                var timelineList = new List<object>();
                foreach (var timeline in score.TimelineTracks)
                {

                    var tackList = timeline.TackPoints.Select(tack => new Dictionary<string, object>
                        {
                            {TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_TACK_TITLE, tack.Title},
                            {TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_TACK_START, tack.Start},
                            {TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_TACK_SPAN, tack.Span}
                        })
                        .Cast<object>()
                        .ToList();

                    var timelineDict = new Dictionary<string, object>{
                        {TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_TIMELINE_TITLE, timeline.Title},
                        {TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_TIMELINE_TACKS, tackList}
                    };

                    timelineList.Add(timelineDict);
                }

                var scoreObject = new Dictionary<string, object>{
                    {TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_SCORE_ID, score.Id},
                    {TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_SCORE_TITLE, score.Title},
                    {TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_SCORE_TIMELINES, timelineList}
                };

                currentScoreList.Add(scoreObject);
            }

            var data = new Dictionary<string, object>{
                {TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_LASTMODIFIED, lastModified.ToString(CultureInfo.InvariantCulture)},
                {TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_SCORES, currentScoreList}
            };

            var dataStr = Json.Serialize(data);
            var targetDirPath = Path.Combine(Application.dataPath, TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_PATH);
            var targetFilePath = Path.Combine(Application.dataPath, TimeFlowShikiSettings.TIMEFLOWSHIKI_DATA_FILEPATH);

            if (!Directory.Exists(targetDirPath))
            {
                Directory.CreateDirectory(targetDirPath);
            }

            using (var sw = new StreamWriter(targetFilePath))
            {
                sw.Write(dataStr);
            }
        }

        /**
			initialize textures.
		*/
        private void InitializeResources()
        {
            TimeFlowShikiGUISettings.TickTex = AssetDatabase.LoadAssetAtPath(TimeFlowShikiGUISettings.RESOURCE_TICK, typeof(Texture2D)) as Texture2D;
            TimeFlowShikiGUISettings.TimelineHeaderTex = AssetDatabase.LoadAssetAtPath(TimeFlowShikiGUISettings.RESOURCE_TRACK_HEADER_BG, typeof(Texture2D)) as Texture2D;
            TimeFlowShikiGUISettings.ConditionLineBgTex = AssetDatabase.LoadAssetAtPath(TimeFlowShikiGUISettings.RESOURCE_CONDITIONLINE_BG, typeof(Texture2D)) as Texture2D;

            TimeFlowShikiGUISettings.FrameTex = AssetDatabase.LoadAssetAtPath(TimeFlowShikiGUISettings.RESOURCE_TRACK_FRAME_BG, typeof(Texture2D)) as Texture2D;

            TimeFlowShikiGUISettings.WhitePointTex = AssetDatabase.LoadAssetAtPath(TimeFlowShikiGUISettings.RESOURCE_TACK_WHITEPOINT, typeof(Texture2D)) as Texture2D;
            TimeFlowShikiGUISettings.GrayPointTex = AssetDatabase.LoadAssetAtPath(TimeFlowShikiGUISettings.RESOURCE_TACK_GRAYPOINT, typeof(Texture2D)) as Texture2D;

            TimeFlowShikiGUISettings.WhitePointSingleTex = AssetDatabase.LoadAssetAtPath(TimeFlowShikiGUISettings.RESOURCE_TACK_WHITEPOINT_SINGLE, typeof(Texture2D)) as Texture2D;
            TimeFlowShikiGUISettings.GrayPointSingleTex = AssetDatabase.LoadAssetAtPath(TimeFlowShikiGUISettings.RESOURCE_TACK_GRAYPOINT_SINGLE, typeof(Texture2D)) as Texture2D;

            TimeFlowShikiGUISettings.ActiveTackBaseTex = AssetDatabase.LoadAssetAtPath(TimeFlowShikiGUISettings.RESOURCE_TACK_ACTIVE_BASE, typeof(Texture2D)) as Texture2D;

            _activeFrameLabelStyle = new GUIStyle { normal = { textColor = Color.white } };

            _activeConditionValueLabelStyle = new GUIStyle
            {
                fontSize = 11,
                normal = { textColor = Color.white }
            };
        }


        private int _drawCounter;
        private void Update()
        {
            _drawCounter++;

            if (_drawCounter % 5 != 0) return;


            if (10000 < _drawCounter) _drawCounter = 0;

            var consumed = false;
            // emit events.
            if (_manipulateEvents.KeyLeft)
            {
                SelectPreviousTack();
                consumed = true;
            }
            if (_manipulateEvents.KeyRight)
            {
                SelectNextTack();
                consumed = true;
            }

            if (_manipulateEvents.KeyUp)
            {
                SelectAheadObject();
                consumed = true;
            }
            if (_manipulateEvents.KeyDown)
            {
                SelectBelowObject();
                consumed = true;
            }

            // renew.
            if (consumed) _manipulateEvents = new ManipulateEvents();
        }

        private void SelectPreviousTack()
        {
            if (!HasValidScore()) return;

            var score = GetActiveScore();

            if (_manipulateTargets.ActiveObjectIds.Any())
            {
                if (_manipulateTargets.ActiveObjectIds.Count == 1)
                {
                    score.SelectPreviousTackOfTimelines(_manipulateTargets.ActiveObjectIds[0]);
                }
                else
                {
                    // select multiple objects.
                }
            }

            if (!_manipulateTargets.ActiveObjectIds.Any()) return;

            var currentSelectedFrame = score.GetStartFrameById(_manipulateTargets.ActiveObjectIds[0]);
            if (0 <= currentSelectedFrame)
            {
                FocusToFrame(currentSelectedFrame);
            }
        }

        private void SelectNextTack()
        {
            if (!HasValidScore()) return;

            var score = GetActiveScore();
            if (_manipulateTargets.ActiveObjectIds.Any())
            {
                if (_manipulateTargets.ActiveObjectIds.Count == 1)
                {
                    score.SelectNextTackOfTimelines(_manipulateTargets.ActiveObjectIds[0]);
                }
                else
                {
                    // select multiple objects.
                }
            }

            if (!_manipulateTargets.ActiveObjectIds.Any()) return;

            var currentSelectedFrame = score.GetStartFrameById(_manipulateTargets.ActiveObjectIds[0]);
            if (0 <= currentSelectedFrame)
            {
                FocusToFrame(currentSelectedFrame);
            }
        }

        private void SelectAheadObject()
        {
            if (!HasValidScore()) return;

            var score = GetActiveScore();

            // if selecting object is top, select tick. unselect all objects.
            if (score.IsActiveTimelineOrContainsActiveObject(0))
            {
                // var activeFrame = score.GetStartFrameById(manipulateTargets.activeObjectIds[0]);

                Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.Unselected, null));
                return;
            }

            if (!_manipulateTargets.ActiveObjectIds.Any()) return;
            score.SelectAboveObjectById(_manipulateTargets.ActiveObjectIds[0]);

            var currentSelectedFrame = score.GetStartFrameById(_manipulateTargets.ActiveObjectIds[0]);
            if (0 <= currentSelectedFrame)
            {
                FocusToFrame(currentSelectedFrame);
            }
        }

        private void SelectBelowObject()
        {
            if (!HasValidScore()) return;

            var score = GetActiveScore();

            if (_manipulateTargets.ActiveObjectIds.Any())
            {
                score.SelectBelowObjectById(_manipulateTargets.ActiveObjectIds[0]);
                var currentSelectedFrame = score.GetStartFrameById(_manipulateTargets.ActiveObjectIds[0]);
                if (0 <= currentSelectedFrame)
                {
                    FocusToFrame(currentSelectedFrame);
                }
                return;
            }

            /*
				choose tack of first timeline under tick.
			*/
            score.SelectTackAtFrame(_selectedFrame);
        }

        /**
			draw GUI
	   	*/
        private void OnGUI()
        {
            var viewWidth = position.width;
            var viewHeight = position.height;

            GUI.BeginGroup(new Rect(0, 0, viewWidth, viewHeight));
            {
                DrawAutoConponent(viewWidth);
            }
            GUI.EndGroup();
        }

        private void DrawAutoConponent(float viewWidth)
        {
            var xScrollIndex = -_scrollPos;
            var yOffsetPos = 0f;


            // draw header.
            var inspectorRect = DrawConditionInspector(xScrollIndex, 0, viewWidth);

            yOffsetPos += inspectorRect.y + inspectorRect.height;

            if (HasValidScore())
            {
                var activeAuto = GetActiveScore();
                // draw timelines
                DrawTimelines(activeAuto, yOffsetPos, xScrollIndex, viewWidth);

                // draw tick
                DrawTick();
            }

            var useEvent = false;


            switch (Event.current.type)
            {
                // mouse event handling.
                case EventType.MouseDown:
                {
                    var touchedFrameCount = TimelineTrack.GetFrameOnTimelineFromAbsolutePosX(_scrollPos + (Event.current.mousePosition.x - TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_SPAN));
                    if (touchedFrameCount < 0) touchedFrameCount = 0;
                    _selectedPos = touchedFrameCount * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH;
                    _selectedFrame = touchedFrameCount;
                    _repaint = true;

                    Emit(new OnTrackEvent(OnTrackEvent.TrackEventType.Unselected, null));

                    useEvent = true;
                    break;
                }
                case EventType.ContextClick:
                {
                    ShowContextMenu();
                    useEvent = true;
                    break;
                }
                case EventType.MouseUp:
                {
                    // right click.
                    if (Event.current.button == 1)
                    {
                        ShowContextMenu();
                        useEvent = true;
                        break;
                    }

                    var touchedFrameCount = TimelineTrack.GetFrameOnTimelineFromAbsolutePosX(_scrollPos + (Event.current.mousePosition.x - TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_SPAN));
                    if (touchedFrameCount < 0) touchedFrameCount = 0;
                    _selectedPos = touchedFrameCount * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH;
                    _selectedFrame = touchedFrameCount;
                    _repaint = true;
                    useEvent = true;
                    break;
                }
                case EventType.MouseDrag:
                {
                    var pos = _scrollPos + (Event.current.mousePosition.x - TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_SPAN);
                    if (pos < 0) pos = 0;
                    _selectedPos = pos - ((TimeFlowShikiGUISettings.TACK_FRAME_WIDTH / 2f) - 1f);
                    _selectedFrame = TimelineTrack.GetFrameOnTimelineFromAbsolutePosX(pos);

                    FocusToFrame(_selectedFrame);

                    _repaint = true;
                    useEvent = true;
                    break;
                }

                // scroll event handling.
                case EventType.ScrollWheel:
                {
                    if (0 != Event.current.delta.x)
                    {
                        _scrollPos = _scrollPos + (Event.current.delta.x * 2);
                        if (_scrollPos < 0) _scrollPos = 0;

                        _repaint = true;
                    }
                    useEvent = true;
                    break;
                }

                // key event handling.
                case EventType.KeyDown:
                {
                    switch (Event.current.keyCode)
                    {
                        case KeyCode.LeftArrow:
                        {
                            if (_manipulateTargets.ActiveObjectIds.Count == 0)
                            {

                                _selectedFrame = _selectedFrame - 1;
                                if (_selectedFrame < 0) _selectedFrame = 0;
                                _selectedPos = _selectedFrame * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH;
                                _repaint = true;

                                FocusToFrame(_selectedFrame);
                            }
                            _manipulateEvents.KeyLeft = true;
                            useEvent = true;
                            break;
                        }
                        case KeyCode.RightArrow:
                        {
                            if (_manipulateTargets.ActiveObjectIds.Count == 0)
                            {
                                _selectedFrame = _selectedFrame + 1;
                                _selectedPos = _selectedFrame * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH;
                                _repaint = true;

                                FocusToFrame(_selectedFrame);
                            }
                            _manipulateEvents.KeyRight = true;
                            useEvent = true;
                            break;
                        }
                        case KeyCode.UpArrow:
                        {
                            _manipulateEvents.KeyUp = true;
                            useEvent = true;
                            break;
                        }
                        case KeyCode.DownArrow:
                        {
                            _manipulateEvents.KeyDown = true;
                            useEvent = true;
                            break;
                        }
                    }
                    break;
                }


            }

            // update cursor pos
            _cursorPos = _selectedPos - _scrollPos;



            if (_repaint) HandleUtility.Repaint();

            if (_eventStacks.Any())
            {
                foreach (var onTrackEvent in _eventStacks) EmitAfterDraw(onTrackEvent);
                _eventStacks.Clear();
                SaveData();
            }

            if (useEvent) Event.current.Use();
        }

        private void ShowContextMenu()
        {
            const int NEAREST_TIMELINE_INDEX = 0; // fixed. should change by mouse position.

            var menu = new GenericMenu();

            if (HasValidScore())
            {
                var currentScore = GetActiveScore();
                var scoreId = currentScore.ScoreId;

                var menuItems = new Dictionary<string, OnTrackEvent.TrackEventType>{
                    {"Add New Timeline", OnTrackEvent.TrackEventType.ScoreAddTimeline}
                };

                foreach (var key in menuItems.Keys)
                {
                    var eventType = menuItems[key];
                    menu.AddItem(
                        new GUIContent(key),
                        false,
                        () => Emit(new OnTrackEvent(eventType, scoreId, NEAREST_TIMELINE_INDEX))
                    );
                }
            }

            menu.ShowAsContext();
        }


        private static ScoreComponent GenerateFirstScore()
        {
            var tackPoints = new List<TackPoint> { new TackPoint(0, TimeFlowShikiGUISettings.DEFAULT_TACK_NAME, 0, 10) };

            var timelines = new List<TimelineTrack> { new TimelineTrack(0, TimeFlowShikiGUISettings.DEFAULT_TIMELINE_NAME, tackPoints) };

            return new ScoreComponent(TimeFlowShikiGUISettings.DEFAULT_SCORE_ID, TimeFlowShikiGUISettings.DEFAULT_SCORE_INFO, timelines);
        }

        private Rect DrawConditionInspector(float xScrollIndex, float yIndex, float inspectorWidth)
        {
            var width = inspectorWidth - TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_SPAN;
            var height = yIndex;

            var assumedHeight = height
                                + TimeFlowShikiGUISettings.CONDITION_INSPECTOR_FRAMECOUNT_HEIGHT
                                + TimeFlowShikiGUISettings.CONDITION_INSPECTOR_FRAMELINE_HEIGHT
                                + AssumeConditionLineHeight();

            GUI.BeginGroup(new Rect(TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_SPAN, height, width, assumedHeight));
            {
                var internalHeight = 0f;

                // count & frame in header.
                {
                    TimelineTrack.DrawFrameBG(xScrollIndex, internalHeight, width, TimeFlowShikiGUISettings.CONDITION_INSPECTOR_FRAMELINE_HEIGHT, true);
                    internalHeight = internalHeight + TimeFlowShikiGUISettings.CONDITION_INSPECTOR_FRAMECOUNT_HEIGHT + TimeFlowShikiGUISettings.CONDITION_INSPECTOR_FRAMELINE_HEIGHT;
                }
                if (HasValidScore())
                {
                    var currentScore = GetActiveScore();
                    var timelines = currentScore.TimelineTracks;
                    foreach (var timeline in timelines)
                    {
                        if (!timeline.IsExistTimeline) continue;
                        internalHeight = internalHeight + TimeFlowShikiGUISettings.CONDITION_INSPECTOR_CONDITIONLINE_SPAN;

                        DrawConditionLine(0, xScrollIndex, timeline, internalHeight);
                        internalHeight = internalHeight + TimeFlowShikiGUISettings.CONDITION_INSPECTOR_CONDITIONLINE_HEIGHT;
                    }

                    if (timelines.Any())
                    {
                        // add footer.
                    }
                }
            }
            GUI.EndGroup();

            return new Rect(0, 0, inspectorWidth, assumedHeight);
        }

        private void DrawTimelines(ScoreComponent activeAuto, float yOffsetPos, float xScrollIndex, float viewWidth)
        {
            BeginWindows();
            activeAuto.DrawTimelines(activeAuto, yOffsetPos, xScrollIndex, viewWidth);
            EndWindows();
        }

        private void DrawTick()
        {
            GUI.BeginGroup(new Rect(TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_SPAN, 0f, position.width - TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_SPAN, position.height));
            {
                // tick
                GUI.DrawTexture(new Rect(_cursorPos + (TimeFlowShikiGUISettings.TACK_FRAME_WIDTH / 2f) - 1f, 0f, 3f, position.height), TimeFlowShikiGUISettings.TickTex);

                // draw frame count.
                if (_selectedFrame == 0)
                {
                    GUI.Label(new Rect(_cursorPos + 5f, 1f, 10f, TimeFlowShikiGUISettings.CONDITION_INSPECTOR_FRAMECOUNT_HEIGHT), "0", _activeFrameLabelStyle);
                }
                else
                {
                    var span = 0;
                    var selectedFrameStr = _selectedFrame.ToString();
                    if (2 < selectedFrameStr.Length) span = ((selectedFrameStr.Length - 2) * 8) / 2;
                    GUI.Label(new Rect(_cursorPos + 2 - span, 1f, selectedFrameStr.Length * 10, TimeFlowShikiGUISettings.CONDITION_INSPECTOR_FRAMECOUNT_HEIGHT), selectedFrameStr, _activeFrameLabelStyle);
                }
            }
            GUI.EndGroup();
        }

        private float AssumeConditionLineHeight()
        {
            var height = 0f;

            if (HasValidScore())
            {
                var currentScore = GetActiveScore();
                var timelines = currentScore.TimelineTracks;
                foreach (TimelineTrack t in timelines)
                {
                    if (!t.IsExistTimeline) continue;
                    height = height + TimeFlowShikiGUISettings.CONDITION_INSPECTOR_CONDITIONLINE_SPAN;
                    height = height + TimeFlowShikiGUISettings.CONDITION_INSPECTOR_CONDITIONLINE_HEIGHT;
                }

                if (timelines.Any())
                {
                    // add footer.
                    height = height + TimeFlowShikiGUISettings.CONDITION_INSPECTOR_CONDITIONLINE_SPAN;
                }
            }

            return height;
        }

        private void DrawConditionLine(float xOffset, float xScrollIndex, TimelineTrack timeline, float yOffset)
        {
            foreach (var tack in timeline.TackPoints)
            {
                if (!tack.IsExistTack) continue;

                var start = tack.Start;
                var span = tack.Span;

                var startPos = xOffset + xScrollIndex + (start * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH);
                var length = span * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH;
                var tex = tack.GetColorTex();

                // draw background.
                if (tack.IsActive())
                {
                    var condtionLineBgRect = new Rect(startPos, yOffset - 1, length, TimeFlowShikiGUISettings.CONDITION_INSPECTOR_CONDITIONLINE_HEIGHT + 2);
                    GUI.DrawTexture(condtionLineBgRect, TimeFlowShikiGUISettings.ConditionLineBgTex);
                }
                else
                {
                    var condtionLineBgRect = new Rect(startPos, yOffset + 1, length, TimeFlowShikiGUISettings.CONDITION_INSPECTOR_CONDITIONLINE_HEIGHT - 2);
                    GUI.DrawTexture(condtionLineBgRect, TimeFlowShikiGUISettings.ConditionLineBgTex);
                }

                // fill color.
                var condtionLineRect = new Rect(startPos + 1, yOffset, length - 2, TimeFlowShikiGUISettings.CONDITION_INSPECTOR_CONDITIONLINE_HEIGHT);
                GUI.DrawTexture(condtionLineRect, tex);
            }

            // draw timelime text
            foreach (var tack in timeline.TackPoints)
            {
                var title = tack.Title;
                var start = tack.Start;
                var span = tack.Span;

                if (start <= _selectedFrame && _selectedFrame < start + span)
                {
                    GUI.Label(new Rect(_cursorPos + (TimeFlowShikiGUISettings.TACK_FRAME_WIDTH / 2f) + 3f, yOffset - 5f, title.Length * 10f, 20f), title, _activeConditionValueLabelStyle);
                }
            }
        }

        private void Emit(OnTrackEvent onTrackEvent)
        {
            var type = onTrackEvent.TrackEvent;
            // tack events.
            switch (type)
            {
                case OnTrackEvent.TrackEventType.Unselected:
                {
                    _manipulateTargets = new ManipulateTargets(new List<string>());

                    Undo.RecordObject(this, "Unselect");

                    var activeAuto = GetActiveScore();
                    activeAuto.DeactivateAllObjects();
                    Repaint();
                    return;
                }
                case OnTrackEvent.TrackEventType.ObjectSelected:
                {
                    _manipulateTargets = new ManipulateTargets(new List<string> { onTrackEvent.ActiveObjectId });

                    var activeAuto = GetActiveScore();

                    Undo.RecordObject(this, "Select");
                    activeAuto.ActivateObjectsAndDeactivateOthers(_manipulateTargets.ActiveObjectIds);
                    Repaint();
                    return;
                }

                /*
					auto events.
				*/
                case OnTrackEvent.TrackEventType.ScoreAddTimeline:
                {
                    var activeAuto = GetActiveScore();
                    var tackPoints = new List<TackPoint>();
                    var newTimeline = new TimelineTrack(activeAuto.TimelineTracks.Count, "New Timeline", tackPoints);

                    Undo.RecordObject(this, "Add Timeline");

                    activeAuto.TimelineTracks.Add(newTimeline);
                    return;
                }


                /*
					timeline events.
				*/
                case OnTrackEvent.TrackEventType.TimelineAddTack:
                {
                    _eventStacks.Add(onTrackEvent.Copy());
                    return;
                }
                case OnTrackEvent.TrackEventType.TimelineDelete:
                {
                    var targetTimelineId = onTrackEvent.ActiveObjectId;
                    var activeAuto = GetActiveScore();

                    Undo.RecordObject(this, "Delete Timeline");

                    activeAuto.DeleteObjectById(targetTimelineId);
                    Repaint();
                    SaveData();
                    return;
                }
                case OnTrackEvent.TrackEventType.TimelineBeforeSave:
                {
                    Undo.RecordObject(this, "Update Timeline Title");
                    return;
                }

                case OnTrackEvent.TrackEventType.TimelineSave:
                {
                    SaveData();
                    return;
                }


                /*
					tack events.
				*/
                case OnTrackEvent.TrackEventType.TackMoving:
                {
                    var movingTackId = onTrackEvent.ActiveObjectId;

                    var activeAuto = GetActiveScore();

                    activeAuto.SetMovingTackToTimelimes(movingTackId);
                    break;
                }
                case OnTrackEvent.TrackEventType.TackMoved:
                {

                    Undo.RecordObject(this, "Move Tack");

                    return;
                }
                case OnTrackEvent.TrackEventType.TackMovedAfter:
                {
                    var targetTackId = onTrackEvent.ActiveObjectId;

                    var activeAuto = GetActiveScore();
                    var activeTimelineIndex = activeAuto.GetTackContainedTimelineIndex(targetTackId);
                    if (0 <= activeTimelineIndex)
                    {
                        activeAuto.TimelineTracks[activeTimelineIndex].UpdateByTackMoved(targetTackId);

                        Repaint();
                        SaveData();
                    }
                    return;
                }
                case OnTrackEvent.TrackEventType.TackDeleted:
                {
                    var targetTackId = onTrackEvent.ActiveObjectId;
                    var activeAuto = GetActiveScore();

                    Undo.RecordObject(this, "Delete Tack");

                    activeAuto.DeleteObjectById(targetTackId);
                    Repaint();
                    SaveData();
                    return;
                }

                case OnTrackEvent.TrackEventType.TackBeforeSave:
                {
                    Undo.RecordObject(this, "Update Tack Title");
                    return;
                }

                case OnTrackEvent.TrackEventType.TackSave:
                {
                    SaveData();
                    return;
                }

                default:
                {
                    Debug.LogError("no match type:" + type);
                    break;
                }
            }
        }


        public void SetActiveScore(int index)
        {
            _scores[index].SetActive();
        }

        /**
			Undo,Redoを元に、各オブジェクトのInspectorの情報を更新する
		*/
        public void ApplyDataToInspector()
        {
            foreach (var score in _scores) score.ApplyDataToInspector();
        }


        private bool HasValidScore()
        {
            return _scores.Any() && _scores.Any(score => score.IsExistScore);
        }

        private ScoreComponent GetActiveScore()
        {
            foreach (var score in _scores)
            {
                if (!score.IsExistScore) continue;
                if (score.IsActive()) return score;
            }
            throw new Exception("no active auto found.");
        }

        private void EmitAfterDraw(OnTrackEvent onTrackEvent)
        {
            var type = onTrackEvent.TrackEvent;
            switch (type)
            {
                case OnTrackEvent.TrackEventType.TimelineAddTack:
                {
                    var targetTimelineId = onTrackEvent.ActiveObjectId;
                    var targetFramePos = onTrackEvent.Frame;

                    var activeAuto = GetActiveScore();

                    Undo.RecordObject(this, "Add Tack");

                    activeAuto.AddNewTackToTimeline(targetTimelineId, targetFramePos);
                    return;
                }
            }
        }



        private void FocusToFrame(int focusTargetFrame)
        {
            var leftFrame = (int)Math.Round(_scrollPos / TimeFlowShikiGUISettings.TACK_FRAME_WIDTH);
            var rightFrame = (int)(((_scrollPos + (position.width - TimeFlowShikiGUISettings.TIMELINE_CONDITIONBOX_SPAN)) / TimeFlowShikiGUISettings.TACK_FRAME_WIDTH) - 1);

            // left edge of view - leftFrame - rightFrame - right edge of view

            if (focusTargetFrame < leftFrame)
            {
                _scrollPos = _scrollPos - ((leftFrame - focusTargetFrame) * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH);
                return;
            }

            if (rightFrame < focusTargetFrame)
            {
                _scrollPos = _scrollPos + ((focusTargetFrame - rightFrame) * TimeFlowShikiGUISettings.TACK_FRAME_WIDTH);
            }
        }



        public static bool IsTimelineId(string activeObjectId)
        {
            return activeObjectId.StartsWith(TimeFlowShikiGUISettings.ID_HEADER_TIMELINE);
        }

        public static bool IsTackId(string activeObjectId)
        {
            return activeObjectId.StartsWith(TimeFlowShikiGUISettings.ID_HEADER_TACK);
        }
    }

}
