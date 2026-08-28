using UnityEngine;

namespace DenEmo.Models
{
    public class ShapeKeyItem
    {
        public int    Index          { get; set; }
        public string Name           { get; set; }
        public float  Value          { get; set; }

        public bool IsIncluded    { get; set; }

        /// <summary>VRCAvatarDescriptor の Eyelids にバインドされているシェイプ（まばたき／視線上下）。</summary>
        public bool IsVrcShape    { get; set; }

        /// <summary>Eyelids の Blink スロット（index 0）にバインドされているシェイプ。</summary>
        public bool IsBlinkShape  { get; set; }

        /// <summary>VRCAvatarDescriptor の LipSync にバインドされているシェイプ（ビセーム／口開閉）。</summary>
        public bool IsLipSyncShape{ get; set; }

        public bool IsFavorite    { get; set; }

        public bool IsVisible     { get; set; }

        public SkinnedMeshRenderer OwnerSmr { get; set; }
        public string              SmrPath  { get; set; }

        public bool IsVrcExcluded(bool isAnimationMode)
        {
            if (!IsVrcShape) return false;
            // アニメーションモードではまばたきシェイプのみ編集を許可する（まばたきキャンセル用）。
            if (isAnimationMode && IsBlinkShape) return false;
            return true;
        }

        public ShapeKeyItem(int index, string name, float initialValue)
        {
            Index          = index;
            Name           = name;
            Value          = initialValue;
            IsIncluded     = true;
            IsVrcShape     = false;
            IsBlinkShape   = false;
            IsLipSyncShape = false;
            IsFavorite     = false;
            IsVisible      = true;
        }
    }
}
