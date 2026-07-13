using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Reaches one hand toward a support point through animator IK — the hand
    /// on the chair back while sitting down / standing up. Added automatically
    /// next to the body's Animator by <see cref="FirstPersonBody"/>; requires
    /// the IK Pass on the animator controller's base layer (the bundled
    /// TemplateCharacter controller ships with it enabled).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class BodyHandSupportIk : MonoBehaviour
    {
        private Animator _animator;
        private Transform _target;
        private float _weight;

        private void Awake() => _animator = GetComponent<Animator>();

        /// <summary>Weight 0 (or a null target) releases the hand back to the animation.</summary>
        public void SetHandTarget(Transform target, float weight)
        {
            _target = target;
            _weight = Mathf.Clamp01(weight);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null) return;
            if (_target == null || _weight <= 0f)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
                return;
            }

            _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, _weight);
            _animator.SetIKPosition(AvatarIKGoal.RightHand, _target.position);
            // Orientation follows the anchor loosely — a firm position with a
            // soft rotation reads as resting the palm, not gripping.
            _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, _weight * 0.6f);
            _animator.SetIKRotation(AvatarIKGoal.RightHand, _target.rotation);
        }
    }
}
