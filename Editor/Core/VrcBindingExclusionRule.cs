using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DenEmo.Models;

namespace DenEmo.Core
{
    /// <summary>
    /// VRCAvatarDescriptor に「実際にバインドされている」シェイプキーだけを除外対象としてマークする。
    /// 名前プレフィックス（vrc.*）による一括除外は行わない：
    /// Unified Expressions / VRCFaceTracking 系アバターでは編集したい表情シェイプにも vrc. が付くため、
    /// プレフィックス判定では巻き添えでリストから消えてしまう。
    /// 競合防止の目的（リップシンク・まばたきを壊さない）は Descriptor のバインドを見れば 100% 満たせる。
    ///
    /// SDK 型へ直接依存せずリフレクションのみで読む（SDK 未導入プロジェクトでもコンパイル可能に保つ）。
    /// </summary>
    public static class VrcBindingExclusionRule
    {
        /// <summary>customEyeLookSettings.eyelidsBlendshapes の Blink スロット。[1]=LookUp, [2]=LookDown。</summary>
        private const int EyelidBlinkSlot = 0;

        private const BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.Instance;

        public static void ApplyExclusion(SkinnedMeshRenderer targetSkinnedMesh, List<ShapeKeyItem> items)
        {
            if (targetSkinnedMesh == null || items == null || items.Count == 0) return;

            var descriptor = VrcAvatarReflection.FindDescriptor(targetSkinnedMesh.transform);
            if (descriptor == null) return;

            ApplyLipSync(descriptor, items);
            ApplyEyelids(descriptor, items);
        }

        // ─── LipSync ──────────────────────────────────────────────────────────

        private static void ApplyLipSync(Component descriptor, List<ShapeKeyItem> items)
        {
            var modeVal = GetMemberValue(descriptor, "lipSync");
            string mode = modeVal != null ? modeVal.ToString() : null;
            if (string.IsNullOrEmpty(mode)) return;

            var smr = GetMemberValue(descriptor, "VisemeSkinnedMesh") as SkinnedMeshRenderer;
            if (smr == null) return;

            if (mode.Contains("VisemeBlendShape"))
            {
                var names = GetMemberValue(descriptor, "VisemeBlendShapes") as string[];
                if (names == null || names.Length == 0) return;

                var set = new HashSet<string>(StringComparer.Ordinal);
                foreach (var n in names)
                    if (!string.IsNullOrEmpty(n)) set.Add(n);
                if (set.Count == 0) return;

                foreach (var item in items)
                    if (item.OwnerSmr == smr && !string.IsNullOrEmpty(item.Name) && set.Contains(item.Name))
                        item.IsLipSyncShape = true;
            }
            else if (mode.Contains("JawFlapBlendShape"))
            {
                // 単一シェイプで口の開閉を駆動するモード。
                var open = GetMemberValue(descriptor, "MouthOpenBlendShapeName") as string;
                if (string.IsNullOrEmpty(open)) return;

                foreach (var item in items)
                    if (item.OwnerSmr == smr && string.Equals(item.Name, open, StringComparison.Ordinal))
                        item.IsLipSyncShape = true;
            }
        }

        // ─── Eyelids (Blink / LookUp / LookDown) ──────────────────────────────

        private static void ApplyEyelids(Component descriptor, List<ShapeKeyItem> items)
        {
            if (GetMemberValue(descriptor, "enableEyeLook") is bool enabled && !enabled) return;

            var settings = GetMemberValue(descriptor, "customEyeLookSettings");
            if (settings == null) return;

            var typeVal = GetMemberValue(settings, "eyelidType");
            string eyelidType = typeVal != null ? typeVal.ToString() : null;
            if (string.IsNullOrEmpty(eyelidType) || !eyelidType.Contains("Blendshape")) return;

            var smr = GetMemberValue(settings, "eyelidsSkinnedMesh") as SkinnedMeshRenderer;
            if (smr == null) return;

            var indices = GetMemberValue(settings, "eyelidsBlendshapes") as int[];
            if (indices == null || indices.Length == 0) return;

            for (int slot = 0; slot < indices.Length; slot++)
            {
                int shapeIndex = indices[slot];
                if (shapeIndex < 0) continue;
                bool isBlink = slot == EyelidBlinkSlot;

                foreach (var item in items)
                {
                    if (item.OwnerSmr != smr || item.Index != shapeIndex) continue;
                    item.IsVrcShape = true;
                    if (isBlink) item.IsBlinkShape = true;
                }
            }
        }

        // ─── Reflection helpers ───────────────────────────────────────────────

        /// <summary>SDK バージョン差異を吸収するため、プロパティ→フィールドの順で同名メンバーを読む。</summary>
        private static object GetMemberValue(object obj, string name)
        {
            if (obj == null) return null;
            var t = obj.GetType();

            var prop = t.GetProperty(name, MemberFlags);
            if (prop != null && prop.CanRead) return prop.GetValue(obj, null);

            var field = t.GetField(name, MemberFlags);
            return field != null ? field.GetValue(obj) : null;
        }
    }
}
