using AnimationEventSystem;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using AnimationEvent = AnimationEventSystem.AnimationEvent;

public class StaticDataEditor : EditorWindow
{
    private static readonly Type _animatorControllerStaticDataType = typeof(AnimatorControllerStaticData);
    private static readonly Type _eventsCollectionType = typeof(EventsCollection);
    private static readonly FieldInfo _animatorControllerStaticData_stateHashToEventsCollectionField = _animatorControllerStaticDataType.GetField("_stateHashToEventsCollection", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo _eventsCollection_animationEventsField = _eventsCollectionType.GetField("_animationEvents", BindingFlags.NonPublic | BindingFlags.Instance);

    private AnimatorControllerStaticData staticData;
    private int eventsCollectionIndex = 0;
    private int animationEventIndex = 0;
    private int eventConditionIndex = 0;
    private bool save = false;
    private bool paramBool = false;
    private int paramInt = 0;
    private float paramFloat = 0f;
    private string paramString = "";

    //Conditions Event
    private int conditionNameEnum = 0;
    private readonly string[] condEnum = { "Int", "Float", "Boolean" };
    private int condType;
    private float condFloat;
    private bool condBool;
    private int condInt;
    private int mode;
    private readonly string[] conditionNameBool = { 
        "Active", 
        "AddAmmoInChamber",
        "AddAmmoInMag",
        "AltFire",
        "Arm",
        "Armed",
        "BoltActionReload",
        "BoltCatch",
        "CanReload",
        "DelAmmoChamber",
        "DelAmmoFromMag",
        "Disarm",
        "Discharge",
        "FastHide",
        "Fire",
        "Grip",
        "Hvat",
        "Idle",
        "IncompatibleAmmo",
        "Inventory",
        "IsExternalMag",
        "LauncherReady",
        "LoadOne",
        "MagFull",
        "MagIn",
        "MagInWeapon",
        "MagOut",
        "MagSwap",
        "ModSet",
        "OffBoltCatch",
        "OnBoltCatch",
        "Patrol",
        "QuickFire",
        "SetFiremode0",
        "SetFiremode1",
        "ShellEject",
        "StockFolded",
        "UseLeftHand",
        "Rechamber",
        "MalfunctionRepair",
        "MisfireSlideUnknown",
        "RollCylinder"
    };
    private readonly string[] conditionNameFloat = {
        "DoubleActionFireModeFloat",
        "Aim_angle",
        "AmmoInChamber",
        "AmmoInMag",
        "AmmoCountForRemove",
        "CharacterID",
        "Deflected",
        "EmptyLinksCount",
        "FireMode",
        "GestureIndex",
        "GripWeight",
        "IdlePosition",
        "IdleVar",
        "LauncherID",
        "LeftHandProgress",
        "RadioCommand",
        "RelTypeNew",
        "RelTypeOld",
        "ShellsInWeapon",
        "ShoulderReach",
        "SpeedDraw",
        "SpeedFix",
        "SpeedReload",
        "StockAnimationIndex",
        "SwingSpeed",
        "ThirdPerson",
        "UnderMod",
        "UseTimeMultiplier",
        "WeaponLevel",
        "CamoraIndex",
        "CamoraIndexForLoadAmmo",
        "CamoraIndexWithShellForRemove",
        "CamoraIndexForRemoveAmmo",
        "CamoraFireIndex",
        "ChamberIndex",
        "ChamberIndexWithShell"
    };
    private readonly string[] conditionNameInt = {
        "AnimationVariant",
        "FireVar",
        "GrenadeAltFire",
        "GrenadeFire",
        "LActionIndex",
        "PlayerState",
        "ThirdAction",
        "Malfunction",
        "MalfType"
    };
    private readonly string[] modeNames = { 
        "Equal",
        "NotEqual",
        "GreaterThan",
        "LessThan",
        "GreaterEqualThan",
        "LessEqualThan" 
    };


    // Temporary function list
    private readonly string[] functionNames = {
        "None",
        "Sound",
        "ThirdAction",
        "UseProp",
        "AddAmmoInChamber",
        "AddAmmoInMag",
        "Arm",
        "Cook",
        "DelAmmoChamber",
        "DelAmmoFromMag",
        "Disarm",
        "FireEnd",
        "FiringBullet",
        "FoldOff",
        "FoldOn",
        "IdleStart",
        "LauncherAppeared",
        "LauncherDisappeared",
        "MagHide",
        "MagIn",
        "MagOut",
        "MagShow",
        "MalfunctionOff",
        "ModChanged",
        "OffBoltCatch",
        "OnBoltCatch",
        "RemoveShell",
        "StartUtilityOperation",
        "ShellEject",
        "WeapIn",
        "WeapOut",
        "OnBackpackDrop"
    };
    private int selectedFunctionIndex = 0;
    private int paramType = 0;
    private readonly string[] paramName = { "None", "Int32", "Float", "String", "Boolean" };

    private bool showEventConditions = false;

    private AnimationClip animationClip;
    private PlayableGraph playableGraph;
    private AnimationClipPlayable playable;
    private bool isPlaying = false;
    private float animationTime = 0f;
    private float lastUpdateTime = 0f;
    private PreviewRenderUtility previewRenderUtility;
    private GameObject previewObject;
    private GameObject userPreviewObject;

    private Vector2 previewDir = new Vector2(120f, -20f);
    private float previewDistance = 5f;
    private Vector3 pivotPoint = Vector3.zero;
    private Light previewLight;

    [MenuItem("Custom Windows/Choccy/Animations/Static Data Editor")]
    public static void ShowWindow()
    {
        GetWindow<StaticDataEditor>("Static Data Editor");
    }

    private void OnEnable()
    {
        playableGraph = PlayableGraph.Create();
        playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        previewRenderUtility = new PreviewRenderUtility() { cameraFieldOfView = 30f };
        previewLight = new GameObject("Preview Light").AddComponent<Light>();
        previewLight.type = LightType.Directional;
        previewLight.intensity = 1.0f;
        previewRenderUtility.AddSingleGO(previewLight.gameObject);
    }

    private void OnDisable()
    {
        if (playableGraph.IsValid())
        {
            playableGraph.Destroy();
        }
        previewRenderUtility?.Cleanup();
        if (previewLight != null)
        {
            DestroyImmediate(previewLight.gameObject);
        }
    }

    private void OnGUI()
    {
        // Static data editing section
        GUILayout.Label("Static Data Editor", EditorStyles.boldLabel);
        staticData = (AnimatorControllerStaticData)EditorGUILayout.ObjectField("Static Data", staticData, typeof(AnimatorControllerStaticData), false);
        eventsCollectionIndex = EditorGUILayout.IntField("Events Collection Index", eventsCollectionIndex);
        animationEventIndex = EditorGUILayout.IntField("Animation Event Index", animationEventIndex);
        if (staticData == null)
        {
            EditorGUILayout.HelpBox("Please assign an AnimatorControllerStaticData object.", MessageType.Warning);
        }
        else
        {
            //DrawStaticDataEditor();
        }

        // Animation preview section
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Animation Clip", EditorStyles.boldLabel);
        animationClip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", animationClip, typeof(AnimationClip), false);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Preview Object", EditorStyles.boldLabel);
        userPreviewObject = (GameObject)EditorGUILayout.ObjectField("User Preview Object", userPreviewObject, typeof(GameObject), false);

        if (animationClip == null)
        {
            EditorGUILayout.HelpBox("Please assign an Animation Clip.", MessageType.Warning);
            return;
        }

        if (GUILayout.Button(isPlaying ? "Stop" : "Play"))
        {
            if (isPlaying)
            {
                StopAnimation();
            }
            else
            {
                PlayAnimation();
            }
        }

        float newProgress = EditorGUILayout.Slider("Progress", animationTime / animationClip.length, 0f, 1f);
        float newAnimationTime = newProgress * animationClip.length;
        if (!isPlaying && newAnimationTime != animationTime)
        {
            animationTime = newAnimationTime;
            playable.SetTime(animationTime);
            playableGraph.Evaluate();
            Repaint();
        }

        Rect previewRect = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        previewRenderUtility.BeginPreview(previewRect, GUIStyle.none);
        HandleCameraControls(previewRect);
        previewRenderUtility.camera.nearClipPlane = 0.01f;
        previewRenderUtility.camera.farClipPlane = 1000f;
        previewLight.transform.position = previewRenderUtility.camera.transform.position;
        previewLight.transform.LookAt(pivotPoint);

        Quaternion camRotation = Quaternion.Euler(previewDir.y, previewDir.x, 0f);
        Vector3 camPosition = pivotPoint - camRotation * Vector3.forward * previewDistance;
        previewRenderUtility.camera.transform.SetPositionAndRotation(camPosition, camRotation);
        previewRenderUtility.Render();
        previewRenderUtility.EndAndDrawPreview(previewRect);

        if (isPlaying)
        {
            float currentTime = Time.realtimeSinceStartup;
            animationTime += currentTime - lastUpdateTime;
            lastUpdateTime = currentTime;
            if (animationTime > animationClip.length)
            {
                animationTime = 0f;
            }
            playable.SetTime(animationTime);
            playableGraph.Evaluate();
            Repaint();
        }
    }

    private void HandleCameraControls(Rect previewRect)
    {
        Event e = Event.current;
        if (previewRect.Contains(e.mousePosition))
        {
            if (e.type == EventType.ScrollWheel)
            {
                previewDistance += e.delta.y * 0.05f;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag)
            {
                if (e.button == 0)
                {
                    previewDir.x += e.delta.x * Mathf.Lerp(0.1f, 1f, previewDistance / 10f);
                    previewDir.y += e.delta.y * Mathf.Lerp(0.1f, 1f, previewDistance / 10f);
                    e.Use();
                }
                else if (e.button == 1)
                {
                    pivotPoint += 0.02f * -e.delta.x * Mathf.Lerp(0.1f, 1f, previewDistance / 75f) * previewRenderUtility.camera.transform.right;
                    pivotPoint -= 0.02f * -e.delta.y * Mathf.Lerp(0.1f, 1f, previewDistance / 75f) * previewRenderUtility.camera.transform.up;
                    e.Use();
                }
            }
        }
    }

    private void PlayAnimation()
    {
        if (previewObject != null)
        {
            DestroyImmediate(previewObject);
        }

        if (userPreviewObject != null)
        {
            previewObject = Instantiate(userPreviewObject);
            previewRenderUtility.AddSingleGO(previewObject);

            if (previewObject.GetComponent<Animator>() == null)
            {
                previewObject.AddComponent<Animator>();
            }
        }
        else
        {
            previewObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            previewRenderUtility.AddSingleGO(previewObject);
        }

        playable = AnimationClipPlayable.Create(playableGraph, animationClip);
        var animator = previewObject.GetComponent<Animator>();
        var output = AnimationPlayableOutput.Create(playableGraph, "Animation", animator);
        output.SetSourcePlayable(playable);
        playableGraph.Play();
        isPlaying = true;
        animationTime = 0f;
        lastUpdateTime = Time.realtimeSinceStartup;
    }
    /*
    private void DrawStaticDataEditor()
    {        
        List<EventsCollection> eventsCollections = _animatorControllerStaticData_stateHashToEventsCollectionField.GetValue(staticData) as List<EventsCollection>;
        while (eventsCollections.Count < eventsCollectionIndex + 1)
        {
            var newEventCollection = new EventsCollection();
            _eventsCollection_animationEventsField.SetValue(newEventCollection, new List<AnimationEvent>()
            {
                new AnimationEvent()
            });
            eventsCollections.Add(newEventCollection);
        }
        EventsCollection eventsCollection = eventsCollections[eventsCollectionIndex];
        AnimationEvent animationEvent = GetOrCreateElement<AnimationEvent>(eventsCollection, "_animationEvents", animationEventIndex);

        // Function name dropdown
        selectedFunctionIndex = EditorGUILayout.Popup("Function Name", selectedFunctionIndex, functionNames);

        switch (functionNames[selectedFunctionIndex])
        {
            case "Sound":
            case "ThirdAction":
            case "UseProp":
                DrawAnimationEventParameter(animationEvent);
                break;
            default:
                ResetEventParam(animationEvent);
                break;
        }
        GUILayout.Label("Conditions", EditorStyles.boldLabel);
        eventConditionIndex = EditorGUILayout.IntField("Event Condition Index", eventConditionIndex);
        // Show event conditions
        showEventConditions = EditorGUILayout.Toggle("Show Event Conditions", showEventConditions);
        GUILayout.Space(5f);
        if (showEventConditions)
        {
            
            if (animationEvent.EventConditions == null)
            {
                animationEvent.EventConditions = new List<EventCondition>();
            }
            EnsureListSize(animationEvent.EventConditions, eventConditionIndex + 1);
            var eventCondition = animationEvent.EventConditions[eventConditionIndex];
            switch (condType = EditorGUILayout.Popup("Condition Type", condType, condEnum))
            {
                case 0:
                    conditionNameEnum = EditorGUILayout.Popup("Name", conditionNameEnum, conditionNameInt);
                    condInt = EditorGUILayout.IntField("Int Value", condInt);
                    mode = EditorGUILayout.Popup("Condition Mode", mode, modeNames);
                    break;
                case 1:
                    conditionNameEnum = EditorGUILayout.Popup("Name", conditionNameEnum, conditionNameFloat);
                    condFloat = EditorGUILayout.FloatField("Float Value", condFloat);
                    mode = EditorGUILayout.Popup("Condition Mode", mode, modeNames);
                    break;
                case 2:
                    conditionNameEnum = EditorGUILayout.Popup("Name", conditionNameEnum, conditionNameBool);
                    condBool = EditorGUILayout.Toggle("Bool Value", condBool);
                    break;
            }
        }

        if (GUILayout.Button("Clear Condition"))
        {
            if (!showEventConditions && animationEvent.EventConditions != null)
            {
                animationEvent.EventConditions.Clear();
            }
        }

        // Add Event button
        if (GUILayout.Button("Add Event"))
        {
            animationEvent.GetType().GetField("_functionName", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(animationEvent, functionNames[selectedFunctionIndex]);
            AddOrUpdateEvent(staticData, eventsCollection, animationEvent);
            //Condition maybe?
            AnimationEvent animationEvents = GetOrCreateElement<AnimationEvent>(eventsCollection, "_animationEvents", animationEventIndex);                        
            if (showEventConditions)
            {
                var eventCondition = animationEvents.EventConditions[eventConditionIndex];
                var condition = eventCondition.GetType().GetField("ParameterName", BindingFlags.Public | BindingFlags.Instance);
                switch (eventCondition.ConditionParamType = (EEventConditionParamTypes)condType)
                {
                    case 0:
                        eventCondition.BoolValue = false;
                        eventCondition.FloatValue = 0;
                        eventCondition.IntValue = condInt;
                        eventCondition.ConditionMode = (EEventConditionModes)mode;
                        condition.SetValue(eventCondition, conditionNameInt[conditionNameEnum]);
                        break;
                    case (EEventConditionParamTypes)1:
                        eventCondition.FloatValue = condFloat;
                        eventCondition.ConditionMode = (EEventConditionModes)mode;
                        eventCondition.BoolValue = false;
                        eventCondition.IntValue = 0;
                        condition.SetValue(eventCondition, conditionNameFloat[conditionNameEnum]);
                        break;
                    case (EEventConditionParamTypes)2:
                        eventCondition.BoolValue = condBool;
                        eventCondition.FloatValue = 0;
                        eventCondition.ConditionMode = 0;
                        eventCondition.IntValue = 0;
                        condition.SetValue(eventCondition, conditionNameBool[conditionNameEnum]);
                        break;
                }
            }
            
        }
    }
    */
    private void DrawAnimationEventParameter(AnimationEvent animationEvent)
    {
        EditorGUILayout.LabelField("Animation Event Parameter", EditorStyles.boldLabel);
        var parameter = animationEvent.Parameter;

        paramBool = EditorGUILayout.Toggle("Bool Param", paramBool);
        paramFloat = EditorGUILayout.FloatField("Float Param", paramFloat);
        paramInt = EditorGUILayout.IntField("Int Param", paramInt);
        paramString = EditorGUILayout.TextField("String Param", paramString);
        paramType = EditorGUILayout.Popup("Param Type", paramType, paramName);

        if (save)
        {
            parameter.BoolParam = paramBool;
            parameter.FloatParam = paramFloat;
            parameter.IntParam = paramInt;
            parameter.StringParam = paramString;
            parameter.ParamType = (EAnimationEventParamType)paramType;
        }
        save = false;
    }

    private T GetOrCreateElement<T>(object obj, string fieldName, int index) where T : new()
    {
        int size = index + 1;
        var list = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj) as List<T>?? new List<T>(size){ new T()};
        EnsureListSize(list, size);
        return list[index];
    }

    private void EnsureListSize<T>(List<T> list, int size) where T : new()
    {
        while (list.Count < size)
        {
            list.Add(new T());
        }
    }

    private void ResetEventParam(AnimationEvent animationEvent)
    {
        var parameter = animationEvent.Parameter;

        if (parameter == null)
        {
            parameter = new AnimationEventParameter();
            animationEvent.Parameter = parameter;
        }
        if (save)
        {
            parameter.BoolParam = false;
            parameter.FloatParam = 0;
            parameter.IntParam = 0;
            parameter.StringParam = "";
            parameter.ParamType = 0;
        }
        save = false;
    }

    private void AddOrUpdateEvent(AnimatorControllerStaticData staticData, EventsCollection eventCollection, AnimationEvent animationEvent)
    {
        var animationEvents = eventCollection.GetType().GetField("_animationEvents", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(eventCollection) as List<AnimationEvent>;

        if (animationEventIndex < animationEvents.Count)
        {
            animationEvents[animationEventIndex] = animationEvent;
        }
        else
        {
            animationEvents.Add(animationEvent);
        }
        save = true;
        animationEvent.GetType().GetField("_time", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(animationEvent, animationTime / animationClip.length);
        EditorUtility.SetDirty(staticData);
        AssetDatabase.SaveAssets();
        CallValidate(staticData);
    }

    private void CallValidate(AnimatorControllerStaticData staticData)
    {
        MethodInfo onValidateMethod = typeof(AnimatorControllerStaticData).GetMethod("OnValidate", BindingFlags.NonPublic | BindingFlags.Instance);
        onValidateMethod?.Invoke(staticData, null);
    }

    private void StopAnimation()
    {
        playableGraph.Stop();
        isPlaying = false;
    }
}