using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DenEmo.Core
{
    /// <summary>
    /// クリップ内の blendShape 以外のカーブをまとめて保持する入れ物。
    /// オブジェクトの表示切替 / ボーン / マテリアル値 / Animator パラメータなどの float カーブに加え、
    /// GetCurveBindings では取得できないオブジェクト参照カーブ（マテリアル差し替え等）も扱う。
    ///
    /// カーブは原形のまま保持・書き戻す（定数化しない）。潰すと元が時間変化するカーブの挙動が
    /// 無音で失われるため、単一フレーム保存でもそのまま書き出す。
    /// </summary>
    public class ExtraCurveSet
    {
        public struct FloatEntry
        {
            public EditorCurveBinding Binding;
            public AnimationCurve     Curve;
        }

        public struct ObjectEntry
        {
            public EditorCurveBinding        Binding;
            public ObjectReferenceKeyframe[] Keys;
        }

        public readonly List<FloatEntry>  FloatCurves  = new List<FloatEntry>();
        public readonly List<ObjectEntry> ObjectCurves = new List<ObjectEntry>();

        public int  Count   => FloatCurves.Count + ObjectCurves.Count;
        public bool IsEmpty => Count == 0;

        /// <summary>SkinnedMeshRenderer の blendShape.* カーブかどうか。</summary>
        public static bool IsBlendShape(EditorCurveBinding b)
        {
            return b.type == typeof(SkinnedMeshRenderer) && b.propertyName.StartsWith("blendShape.");
        }

        /// <summary>クリップから blendShape 以外の全カーブを収集する。</summary>
        public static ExtraCurveSet Capture(AnimationClip clip)
        {
            var set = new ExtraCurveSet();
            if (clip == null) return set;

            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                if (IsBlendShape(b)) continue;
                var curve = AnimationUtility.GetEditorCurve(clip, b);
                if (curve == null) continue;
                set.FloatCurves.Add(new FloatEntry { Binding = b, Curve = curve });
            }

            // オブジェクト参照カーブは float カーブとは別 API でしか取得できない
            foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, b);
                if (keys == null || keys.Length == 0) continue;
                set.ObjectCurves.Add(new ObjectEntry { Binding = b, Keys = keys });
            }

            return set;
        }

        /// <summary>
        /// 保持しているカーブをクリップへ書き戻す。
        /// overwriteExisting = false のとき、書き込み先に同じバインディングが既にあればそちらを優先する
        /// （既に維持されている書き込み先のデータを取り込み分で壊さないため）。
        /// </summary>
        public void WriteTo(AnimationClip clip, bool overwriteExisting)
        {
            if (clip == null || IsEmpty) return;

            HashSet<string> present = null;
            if (!overwriteExisting)
            {
                present = new HashSet<string>();
                foreach (var b in AnimationUtility.GetCurveBindings(clip))
                    present.Add(BindingKey(b));
                foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    present.Add(BindingKey(b));
            }

            foreach (var e in FloatCurves)
            {
                if (present != null && present.Contains(BindingKey(e.Binding))) continue;
                AnimationUtility.SetEditorCurve(clip, e.Binding, e.Curve);
            }

            foreach (var e in ObjectCurves)
            {
                if (present != null && present.Contains(BindingKey(e.Binding))) continue;
                AnimationUtility.SetObjectReferenceCurve(clip, e.Binding, e.Keys);
            }
        }

        /// <summary>
        /// クリップから blendShape カーブだけを取り除く。
        /// AnimationClip.ClearCurves() と違い、オブジェクト参照カーブを含む他のカーブを壊さない。
        /// </summary>
        public static void ClearBlendShapeCurves(AnimationClip clip)
        {
            if (clip == null) return;
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                if (IsBlendShape(b)) AnimationUtility.SetEditorCurve(clip, b, null);
            }
        }

        private static string BindingKey(EditorCurveBinding b)
        {
            return b.path + "\n" + (b.type != null ? b.type.FullName : "?") + "\n" + b.propertyName;
        }
    }
}
