using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Combat.Editor
{
    public static class SwordLocomotionTools
    {
        private const string MovementFolder = "Assets/Game/Art/animation/DoubleL/FBX Unity/One Hand Up/Movement/";
        private const string ControllerFolder = "Assets/Game/Art/animation/Controllers/";

        [MenuItem("Tools/Game/Animation/Rebuild Sword Locomotion Posture")]
        public static void Bake()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请退出运行模式后调整动画。");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ControllerFolder + "SingleHandSwordAnimatorOverrides.overrideController");
            if (controller == null) throw new InvalidOperationException("缺少单手剑动画覆盖控制器。");
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(overrides);
            var originals = new[] { "Armature|Idle_Loop", "Armature|Jog_Fwd_Loop", "Armature|Sprint_Loop" };
            var sourcePaths = new[]
            {
                "Idle/Idle/1Hand_Up_Stand_Idle_A_2.fbx",
                "Run/Base/1Hand_Up_Run_A_F.fbx",
                "Sprint/Base/1Hand_Up_Sprint_A_F.fbx"
            };
            var names = new[] { "SingleHandSwordIdle", "SingleHandSwordRun", "SingleHandSwordSprint" };
            var uprightDegrees = new[] { 4f, 6f, 8f };
            var indices = originals.Select(name => overrides.FindIndex(pair => pair.Key.name == name)).ToArray();
            if (indices.Any(index => index < 0)) throw new InvalidOperationException("缺少待机、跑步或冲刺的基础动画。");
            var sources = sourcePaths.Select(path => AssetDatabase.LoadAllAssetsAtPath(MovementFolder + path)
                .OfType<AnimationClip>().First(clip => !clip.name.StartsWith("__") && clip.humanMotion)).ToArray();

            Undo.RecordObject(controller, "Adjust Sword Locomotion Posture");
            for (int i = 0; i < sources.Length; i++)
            {
                var clip = UnityEngine.Object.Instantiate(sources[i]);
                clip.name = names[i];
                try
                {
                    RotateBody(clip, uprightDegrees[i]);
                    string path = ControllerFolder + names[i] + ".anim";
                    var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (existing == null)
                    {
                        AssetDatabase.CreateAsset(clip, path);
                        existing = clip;
                    }
                    else
                    {
                        Undo.RecordObject(existing, "Adjust Sword Locomotion Posture");
                        EditorUtility.CopySerialized(clip, existing);
                        EditorUtility.SetDirty(existing);
                    }
                    overrides[indices[i]] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[indices[i]].Key, existing);
                }
                finally
                {
                    if (!AssetDatabase.Contains(clip)) UnityEngine.Object.DestroyImmediate(clip);
                }
            }
            controller.ApplyOverrides(overrides);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        private static void RotateBody(AnimationClip clip, float uprightDegrees)
        {
            var bindings = AnimationUtility.GetCurveBindings(clip).Where(binding => binding.propertyName.StartsWith("RootQ."))
                .OrderBy(binding => "xyzw".IndexOf(binding.propertyName.Last())).ToArray();
            if (bindings.Length != 4) throw new InvalidOperationException("动画缺少完整的 Humanoid 身体旋转曲线。");
            var curves = bindings.Select(binding => AnimationUtility.GetEditorCurve(clip, binding)).ToArray();
            var keys = curves.Select(curve => curve.keys).ToArray();
            var correction = Quaternion.Euler(-uprightDegrees, 0f, 0f);
            for (int key = 0; key < keys[0].Length; key++)
            {
                if (keys.Any(component => component.Length != keys[0].Length || component[key].time != keys[0][key].time))
                    throw new InvalidOperationException("身体旋转曲线的关键帧时间不一致。");
                var value = correction * new Quaternion(keys[0][key].value, keys[1][key].value, keys[2][key].value, keys[3][key].value);
                var incoming = correction * new Quaternion(keys[0][key].inTangent, keys[1][key].inTangent, keys[2][key].inTangent, keys[3][key].inTangent);
                var outgoing = correction * new Quaternion(keys[0][key].outTangent, keys[1][key].outTangent, keys[2][key].outTangent, keys[3][key].outTangent);
                for (int component = 0; component < 4; component++)
                {
                    keys[component][key].value = value[component];
                    keys[component][key].inTangent = incoming[component];
                    keys[component][key].outTangent = outgoing[component];
                }
            }
            for (int component = 0; component < 4; component++) curves[component].keys = keys[component];
            AnimationUtility.SetEditorCurves(clip, bindings, curves);
        }
    }
}
