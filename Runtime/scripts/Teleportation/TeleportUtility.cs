using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Moves a transform the way a teleport does, for systems that place an object themselves
    /// instead of going through a teleport event - SceneManagement's scenario reset puts props
    /// back with it. Same CharacterController handling as <see cref="TeleportOnEvent"/>.
    /// </summary>
    public static class TeleportUtility
    {
        /// <summary>
        /// Places <paramref name="subject"/> at the given pose. A CharacterController must be
        /// disabled while its transform is written, or it snaps the transform straight back.
        /// </summary>
        /// <param name="resetVelocity">
        /// Also zero a non-kinematic Rigidbody's velocities. Off for ordinary teleports (the
        /// historical behavior), on when an object is being restored to a saved pose and should
        /// not keep the momentum it had somewhere else.
        /// </param>
        public static void PlaceAt(GameObject subject, Vector3 position, Quaternion rotation, bool resetVelocity = false)
        {
            if (subject == null) return;

            var characterController = subject.GetComponent<CharacterController>();
            if (characterController != null) characterController.enabled = false;

            subject.transform.SetPositionAndRotation(position, rotation);

            if (resetVelocity && subject.TryGetComponent<Rigidbody>(out var rigidbody) && !rigidbody.isKinematic)
            {
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            }

            if (characterController != null) characterController.enabled = true;
        }
    }
}
