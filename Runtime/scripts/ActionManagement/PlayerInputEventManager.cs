using UnityEngine;
using UnityEditor;
using jeanf.EventSystem;
using Unity.XR.CoreUtils;

namespace jeanf.vrplayer
{
    public class PlayerInputEventManager : MonoBehaviour
    {
        #region event scriptable objects
        private VoidEventChannelSO grab_LeftHand_SetAttachTransformSO;
        private VoidEventChannelSO grab_RightHand_SetAttachTransformSO;
        private IntEventChannelSO grabCountEventChannelSO;
        private BoolEventChannelSO grabEvent_LeftHandStateSO;
        private BoolEventChannelSO grabEvent_RightHandStateSO;
        private VoidEventChannelSO noHandGrabbingSO;
        private VoidEventChannelSO actionDeniedSO;
        private BoolEventChannelSO cursorStateChannel_ManualOverrideSO;
        private IntEventChannelSO floorChangeSO;
        private IntEventChannelSO floorInaccessibleSO;
        private BoolEventChannelSO gloveStateEventChannelSO;
        private BoolEventChannelSO HMDStateEventChannelSO;
        private VoidEventChannelSO mouseLookCameraResentEventChannelSO;
        private BoolEventChannelSO mouseLookStateEventChannelSO;
        private BoolEventChannelSO primaryItemStateEventChannelSO;
        private BoolEventChannelSO primaryItemStateEventVR_ChannelSO;
        private XRBaseInteractorEventChannelSO xrDirectInteractorEvent_LeftChannelSO;
        private XRBaseInteractorEventChannelSO xrDirectInteractorEvent_RightChannelSO;
        #endregion



        public void CreateEventChannels()
        {
            const string searching = "attempting to find";
            const string pathGrabFolder = "Player/Channels/Grab";
            const string pathChannelsFolder = "Player/Channels";
            const string searchLocation = "the resources folder";


            #region Events to go in Assets/Resources/Player/Channels folder
            if (actionDeniedSO == null)
            {
                actionDeniedSO = Resources.Load<VoidEventChannelSO>($"{pathChannelsFolder}/ActionDenied");
                if (actionDeniedSO == null)
                {
                    Debug.Log($"Did not found channel, creating in folder {pathChannelsFolder}");
                    actionDeniedSO = (VoidEventChannelSO)ScriptableObject.CreateInstance($"VoidEventChannelSO");
                    actionDeniedSO.name = "ActionDenied";
                    AssetDatabase.CreateAsset(actionDeniedSO, $"Assets/Resources/{pathChannelsFolder}/ActionDenied.asset");
                }
            }

            if (cursorStateChannel_ManualOverrideSO == null)
            {
                cursorStateChannel_ManualOverrideSO = Resources.Load<BoolEventChannelSO>($"{pathChannelsFolder}/CursorStateChannel_ManualOverride");
                if (cursorStateChannel_ManualOverrideSO == null)
                {
                    Debug.Log($"Did not found channel, creating in folder {pathChannelsFolder}");
                    cursorStateChannel_ManualOverrideSO = (BoolEventChannelSO)ScriptableObject.CreateInstance($"BoolEventChannelSO");
                    cursorStateChannel_ManualOverrideSO.name = "CursorStateChannel_ManualOverride";
                    AssetDatabase.CreateAsset(cursorStateChannel_ManualOverrideSO, $"Assets/Resources/{pathChannelsFolder}/CursorStateChannel_ManualOverride.asset");
                }
            }

            if (floorChangeSO == null)
            {
                floorChangeSO = Resources.Load<IntEventChannelSO>($"{pathChannelsFolder}/FloorChange");
                if (floorChangeSO == null)
                {
                    Debug.Log($"Did not found channel, creating in folder {pathChannelsFolder}");
                    floorChangeSO = (IntEventChannelSO)ScriptableObject.CreateInstance($"IntEventChannelSO");
                    floorChangeSO.name = "FloorChange";
                    AssetDatabase.CreateAsset(floorChangeSO, $"Assets/Resources/{pathChannelsFolder}/FloorChange.asset");
                }
            }

            if (floorInaccessibleSO == null)
            {
                floorInaccessibleSO = Resources.Load<IntEventChannelSO>($"{pathChannelsFolder}/FloorInaccessible");
                if (floorInaccessibleSO == null)
                {
                    Debug.Log($"Did not found channel, creating in folder {pathChannelsFolder}");
                    floorInaccessibleSO = (IntEventChannelSO)ScriptableObject.CreateInstance($"IntEventChannelSO");
                    floorInaccessibleSO.name = "FloorInaccessible";
                    AssetDatabase.CreateAsset(floorInaccessibleSO, $"Assets/Resources/{pathChannelsFolder}/FloorInaccessible.asset");
                }
            }

            if (gloveStateEventChannelSO == null)
            {
                gloveStateEventChannelSO = Resources.Load<BoolEventChannelSO>($"{pathChannelsFolder}/GloveState Event Channel SO");
                if (gloveStateEventChannelSO == null)
                {
                    Debug.Log($"Did not found channel, creating in folder {pathChannelsFolder}");
                    gloveStateEventChannelSO = (BoolEventChannelSO)ScriptableObject.CreateInstance($"BoolEventChannelSO");
                    gloveStateEventChannelSO.name = "GloveState Event Channel SO";
                    AssetDatabase.CreateAsset(gloveStateEventChannelSO, $"Assets/Resources/{pathChannelsFolder}/GloveState Event Channel SO.asset");
                }
            }

            if (HMDStateEventChannelSO == null)
            {
                HMDStateEventChannelSO = Resources.Load<BoolEventChannelSO>($"{pathChannelsFolder}/HMDState Event Channel SO");
                if (HMDStateEventChannelSO == null)
                {
                    Debug.Log($"Did not found channel, creating in folder {pathChannelsFolder}");
                    HMDStateEventChannelSO = (BoolEventChannelSO)ScriptableObject.CreateInstance($"BoolEventChannelSO");
                    HMDStateEventChannelSO.name = "HMDState Event Channel SO";
                    AssetDatabase.CreateAsset(HMDStateEventChannelSO, $"Assets/Resources/{pathChannelsFolder}/HMDState Event Channel SO.asset");
                }
            }

            if (mouseLookCameraResentEventChannelSO == null)
            {
                mouseLookCameraResentEventChannelSO = Resources.Load<VoidEventChannelSO>($"{pathChannelsFolder}/MouseLookCameraReset Event Channel SO");
                if (mouseLookCameraResentEventChannelSO == null)
                {
                    Debug.Log($"Did not found channel, creating in folder {pathChannelsFolder}");
                    mouseLookCameraResentEventChannelSO = (VoidEventChannelSO)ScriptableObject.CreateInstance($"VoidEventChannelSO");
                    mouseLookCameraResentEventChannelSO.name = "MouseLookCameraReset Event Channel SO";
                    AssetDatabase.CreateAsset(mouseLookCameraResentEventChannelSO, $"Assets/Resources/{pathChannelsFolder}/MouseLookCameraReset Event Channel SO.asset");
                }
            }

            if (mouseLookStateEventChannelSO == null)
            {
                mouseLookStateEventChannelSO = Resources.Load<BoolEventChannelSO>($"{pathChannelsFolder}/MouseLookState Event Channel SO");
                if (mouseLookStateEventChannelSO == null)
                {
                    Debug.Log($"Did not found channel, creating in folder {pathChannelsFolder}");
                    mouseLookStateEventChannelSO = (BoolEventChannelSO)ScriptableObject.CreateInstance($"BoolEventChannelSO");
                    mouseLookStateEventChannelSO.name = "MouseLookState Event Channel SO";
                    AssetDatabase.CreateAsset(mouseLookStateEventChannelSO, $"Assets/Resources/{pathChannelsFolder}/MouseLookState Event Channel SO.asset");
                }
            }

            if (primaryItemStateEventChannelSO == null)
            {
                primaryItemStateEventChannelSO = Resources.Load<BoolEventChannelSO>($"{pathChannelsFolder}/PrimaryItemState Event Channel SO");
                if (primaryItemStateEventChannelSO == null)
                {
                    Debug.Log($"Did not found channel, creating in folder {pathChannelsFolder}");
                    primaryItemStateEventChannelSO = (BoolEventChannelSO)ScriptableObject.CreateInstance($"BoolEventChannelSO");
                    primaryItemStateEventChannelSO.name = "PrimaryItemState Event Channel SO";
                    AssetDatabase.CreateAsset(primaryItemStateEventChannelSO, $"Assets/Resources/{pathChannelsFolder}/PrimaryItemState Event Channel SO.asset");
                }
            }

            if (primaryItemStateEventVR_ChannelSO == null)
            {
                primaryItemStateEventVR_ChannelSO = Resources.Load<BoolEventChannelSO>($"{pathChannelsFolder}/PrimaryItemState Event VR_Channel SO");
                if (primaryItemStateEventVR_ChannelSO == null)
                {
                    Debug.Log($"Did not found channel, creating in folder {pathChannelsFolder}");
                    primaryItemStateEventVR_ChannelSO = (BoolEventChannelSO)ScriptableObject.CreateInstance($"BoolEventChannelSO");
                    primaryItemStateEventVR_ChannelSO.name = "PrimaryItemState Event VR_Channel SO";
                    AssetDatabase.CreateAsset(primaryItemStateEventVR_ChannelSO, $"Assets/Resources/{pathChannelsFolder}/PrimaryItemState Event VR_Channel SO.asset");
                }
            }

            if (xrDirectInteractorEvent_LeftChannelSO == null)
            {
                xrDirectInteractorEvent_LeftChannelSO = Resources.Load<XRBaseInteractorEventChannelSO>($"{pathChannelsFolder}/XRDirectInteractorEvent_LeftChannel SO");
                if (xrDirectInteractorEvent_LeftChannelSO == null)
                {
                    Debug.Log($"Did not found channel, creating in folder {pathChannelsFolder}");
                    xrDirectInteractorEvent_LeftChannelSO = (XRBaseInteractorEventChannelSO)ScriptableObject.CreateInstance($"XRBaseInteractorEventChannelSO");
                    xrDirectInteractorEvent_LeftChannelSO.name = "XRDirectInteractorEvent_LeftChannel SO";
                    AssetDatabase.CreateAsset(xrDirectInteractorEvent_LeftChannelSO, $"Assets/Resources/{pathChannelsFolder}/XRDirectInteractorEvent_LeftChannel SO.asset");
                }
            }

            if (xrDirectInteractorEvent_RightChannelSO == null)
            {
                xrDirectInteractorEvent_RightChannelSO = Resources.Load<XRBaseInteractorEventChannelSO>($"{pathChannelsFolder}/XRDirectInteractorEvent_RightChannel SO");
                if (xrDirectInteractorEvent_RightChannelSO == null)
                {
                    Debug.Log($"Did not found channel, creating in folder {pathChannelsFolder}");
                    xrDirectInteractorEvent_RightChannelSO = (XRBaseInteractorEventChannelSO)ScriptableObject.CreateInstance($"XRBaseInteractorEventChannelSO");
                    xrDirectInteractorEvent_RightChannelSO.name = "XRDirectInteractorEvent_RightChannel SO";
                    AssetDatabase.CreateAsset(xrDirectInteractorEvent_RightChannelSO, $"Assets/Resources/{pathChannelsFolder}/XRDirectInteractorEvent_RightChannel SO.asset");
                }
            }

            #endregion

            #region Events to go in Assets/Resources/Player/Channels/Grab folder
            if (grab_LeftHand_SetAttachTransformSO == null)
            {
                grab_LeftHand_SetAttachTransformSO = Resources.Load<VoidEventChannelSO>($"{pathGrabFolder}/Grab_LeftHand_SetAttachTransformSO");
                if (grab_LeftHand_SetAttachTransformSO == null)
                {
                    Debug.Log($"Did not found channel, creating in folder {pathGrabFolder}");
                    grab_LeftHand_SetAttachTransformSO = (VoidEventChannelSO)ScriptableObject.CreateInstance($"VoidEventChannelSO");
                    grab_LeftHand_SetAttachTransformSO.name = "Grab_LeftHand_SetAttachTransform";
                    AssetDatabase.CreateAsset(grab_LeftHand_SetAttachTransformSO, $"Assets/Resources/{pathGrabFolder}/Grab_LeftHand_SetAttachTransformSO.asset");
                }
            }

            if (grab_RightHand_SetAttachTransformSO == null)
            {
                grab_RightHand_SetAttachTransformSO = Resources.Load<VoidEventChannelSO>($"{pathGrabFolder}/Grab_RightHand_SetAttachTransformSO");
                if (grab_RightHand_SetAttachTransformSO == null)
                {
                    Debug.Log($"Did not found channel, creating in folder {pathGrabFolder}");
                    grab_RightHand_SetAttachTransformSO = (VoidEventChannelSO)ScriptableObject.CreateInstance($"VoidEventChannelSO");
                    grab_RightHand_SetAttachTransformSO.name = "Grab_RightHand_SetAttachTransform";
                    AssetDatabase.CreateAsset(grab_RightHand_SetAttachTransformSO, $"Assets/Resources/{pathGrabFolder}/Grab_RightHand_SetAttachTransformSO.asset");
                }
            }

            if (grabCountEventChannelSO == null)
            {
                grabCountEventChannelSO = Resources.Load<IntEventChannelSO>($"{pathGrabFolder}/GrabCount Event Channel SO");
                if (grabCountEventChannelSO == null)
                {
                    Debug.Log($"Did not found channel, creating in folder {pathGrabFolder}");
                    grabCountEventChannelSO = (IntEventChannelSO)ScriptableObject.CreateInstance($"IntEventChannelSO");
                    grabCountEventChannelSO.name = "GrabCount Event Channel SO";
                    AssetDatabase.CreateAsset(grabCountEventChannelSO, $"Assets/Resources/{pathGrabFolder}/GrabCount Event Channel SO.asset");
                }
            }

            if (grabEvent_LeftHandStateSO == null)
            {
                grabEvent_LeftHandStateSO = Resources.Load<BoolEventChannelSO>($"{pathGrabFolder}/GrabEvent_LeftHandState");
                if (grabEvent_LeftHandStateSO == null)
                {
                    Debug.Log($"Did not found channel, creating in folder {pathGrabFolder}");
                    grabEvent_LeftHandStateSO = (BoolEventChannelSO)ScriptableObject.CreateInstance($"BoolEventChannelSO");
                    grabEvent_LeftHandStateSO.name = "GrabEvent_LeftHandState";
                    AssetDatabase.CreateAsset(grabEvent_LeftHandStateSO, $"Assets/Resources/{pathGrabFolder}/GrabEvent_LeftHandState.asset");
                }
            }

            if (grabEvent_RightHandStateSO == null)
            {
                grabEvent_RightHandStateSO = Resources.Load<BoolEventChannelSO>($"{pathGrabFolder}/GrabEvent_RightHandState");
                if (grabEvent_RightHandStateSO == null)
                {
                    Debug.Log($"Did not found channel, creating in folder {pathGrabFolder}");
                    grabEvent_RightHandStateSO = (BoolEventChannelSO)ScriptableObject.CreateInstance($"BoolEventChannelSO");
                    grabEvent_RightHandStateSO.name = "GrabEvent_RightHandState";
                    AssetDatabase.CreateAsset(grabEvent_RightHandStateSO, $"Assets/Resources/{pathGrabFolder}/GrabEvent_RightHandState.asset");
                }
            }

            if (noHandGrabbingSO == null)
            {
                noHandGrabbingSO = Resources.Load<VoidEventChannelSO>($"{pathGrabFolder}/NoHandGrabbing");
                if (noHandGrabbingSO == null)
                {
                    Debug.Log($"Did not found channel, creating in folder {pathGrabFolder}");
                    noHandGrabbingSO = (VoidEventChannelSO)ScriptableObject.CreateInstance($"VoidEventChannelSO");
                    noHandGrabbingSO.name = "NoHandGrabbing";
                    AssetDatabase.CreateAsset(noHandGrabbingSO, $"Assets/Resources/{pathGrabFolder}/NoHandGrabbing.asset");
                }
            }
            #endregion



        }
    }

}
