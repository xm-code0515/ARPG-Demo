using System;
using System.Linq;
using Game.Combat.Unity;
using UnityEditor;
using UnityEngine;

namespace Game.Combat.Editor
{
    public static class MeleeAttackPatternTools
    {
        [MenuItem("CONTEXT/MeleeAttackPattern/Bake Animation Movement")]
        private static void Bake(MenuCommand command)
        {
            var pattern = (MeleeAttackPattern)command.context;
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/NetworkPlayer.prefab");
            float scale = player.GetComponentInChildren<Animator>().humanScale;
            var samples = new Vector2[pattern.Attacks.Length][];
            for (int i = 0; i < samples.Length; i++)
            {
                var clip = pattern.Attacks[i].animation;
                if (clip == null || !clip.humanMotion) throw new InvalidOperationException("攻击动作必须引用 Humanoid AnimationClip。");
                var bindings = AnimationUtility.GetCurveBindings(clip);
                var x = AnimationUtility.GetEditorCurve(clip, bindings.First(b => b.propertyName == "RootT.x"));
                var z = AnimationUtility.GetEditorCurve(clip, bindings.First(b => b.propertyName == "RootT.z"));
                float duration = Mathf.Clamp(pattern.Attacks[i].duration, .05f, clip.length);
                if (pattern.Attacks[i].hitTime >= duration)
                    throw new InvalidOperationException("命中时机必须早于攻击结束时间。");
                int count = Mathf.CeilToInt(duration * 60) + 1;
                samples[i] = new Vector2[count];
                for (int j = 1; j < count; j++)
                {
                    float time = duration * j / (count - 1f);
                    samples[i][j] = new Vector2(x.Evaluate(time) - x.Evaluate(0), z.Evaluate(time) - z.Evaluate(0)) * scale;
                }
            }
            Undo.RecordObject(pattern, "Bake Animation Movement");
            for (int i = 0; i < samples.Length; i++)
            {
                pattern.Attacks[i].duration = Mathf.Min(pattern.Attacks[i].duration, pattern.Attacks[i].animation.length);
                pattern.Attacks[i].rootMotionSamples = samples[i];
            }
            EditorUtility.SetDirty(pattern);
            AssetDatabase.SaveAssets();
        }
    }
}
