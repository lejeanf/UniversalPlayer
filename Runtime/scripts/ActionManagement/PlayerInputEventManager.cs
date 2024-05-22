using System;
using UnityEngine;
using UnityEditor;
using jeanf.EventSystem;

namespace jeanf.vrplayer
{
    public class PlayerInputEventManager : MonoBehaviour
    {
        #region event scriptable objects
        private BoolEventChannelSO takeObject;
        private BoolEventChannelSO cursorStateChannel_ManualOverrideSO;
        private IntBoolEventChannelSO takeObjectInSpecificLocation;
        private VoidEventChannelSO grab_LeftHand_SetAttachTransformSO;
        private VoidEventChannelSO grab_RightHand_SetAttachTransformSO;
        private IntEventChannelSO grabCountEventChannelSO;
        private BoolEventChannelSO grabEvent_LeftHandStateSO;
        private BoolEventChannelSO grabEvent_RightHandStateSO;
        private VoidEventChannelSO noHandGrabbingSO;
        private VoidEventChannelSO actionDeniedSO;
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


        #if UNITY_EDITOR
        // ReSharper disable Unity.PerformanceAnalysis
        public void CreateEventChannels()
        {
            const string pathGrabFolder = "Player/Channels/Grab";
            const string pathChannelsFolder = "Player/Channels";

            #region Events to go in Assets/Resources/Player/Channels folder

            var path = pathChannelsFolder;

            takeObject                             = CreateSO<BoolEventChannelSO>( nameof(takeObject),typeof(BoolEventChannelSO),  path) as BoolEventChannelSO;
            cursorStateChannel_ManualOverrideSO    = CreateSO<BoolEventChannelSO>( nameof(cursorStateChannel_ManualOverrideSO), typeof(BoolEventChannelSO),  path) as BoolEventChannelSO;
            actionDeniedSO                         = CreateSO<VoidEventChannelSO>( nameof(actionDeniedSO),typeof(VoidEventChannelSO), path) as VoidEventChannelSO;
            takeObjectInSpecificLocation           = CreateSO<IntBoolEventChannelSO>( nameof(takeObjectInSpecificLocation), typeof(IntBoolEventChannelSO), path) as IntBoolEventChannelSO;
            actionDeniedSO                         = CreateSO<VoidEventChannelSO>( nameof(actionDeniedSO), typeof(VoidEventChannelSO),path) as VoidEventChannelSO;
            cursorStateChannel_ManualOverrideSO    = CreateSO<BoolEventChannelSO>( nameof(cursorStateChannel_ManualOverrideSO), typeof(BoolEventChannelSO),path) as BoolEventChannelSO;
            floorChangeSO                          = CreateSO<IntEventChannelSO>( nameof(floorChangeSO), typeof(IntEventChannelSO),path) as IntEventChannelSO;
            floorInaccessibleSO                    = CreateSO<IntEventChannelSO>( nameof(floorInaccessibleSO), typeof(IntEventChannelSO),path) as IntEventChannelSO;
            gloveStateEventChannelSO               = CreateSO<BoolEventChannelSO>( nameof(gloveStateEventChannelSO),typeof(BoolEventChannelSO),path) as BoolEventChannelSO;
            HMDStateEventChannelSO                 = CreateSO<BoolEventChannelSO>( nameof(HMDStateEventChannelSO), typeof(BoolEventChannelSO),path) as BoolEventChannelSO;
            mouseLookCameraResentEventChannelSO    = CreateSO<VoidEventChannelSO>( nameof(mouseLookCameraResentEventChannelSO), typeof(VoidEventChannelSO),path) as VoidEventChannelSO;
            mouseLookStateEventChannelSO           = CreateSO<BoolEventChannelSO>( nameof(mouseLookStateEventChannelSO), typeof(BoolEventChannelSO),path) as BoolEventChannelSO;
            primaryItemStateEventChannelSO         = CreateSO<BoolEventChannelSO>( nameof(primaryItemStateEventChannelSO), typeof(BoolEventChannelSO), path) as BoolEventChannelSO;
            primaryItemStateEventVR_ChannelSO      = CreateSO<BoolEventChannelSO>( nameof(primaryItemStateEventVR_ChannelSO),typeof(BoolEventChannelSO), path) as BoolEventChannelSO;
            xrDirectInteractorEvent_LeftChannelSO  = CreateSO<XRBaseInteractorEventChannelSO>( nameof(xrDirectInteractorEvent_LeftChannelSO), typeof(XRBaseInteractorEventChannelSO), path) as XRBaseInteractorEventChannelSO;
            xrDirectInteractorEvent_RightChannelSO = CreateSO<XRBaseInteractorEventChannelSO>(nameof(xrDirectInteractorEvent_RightChannelSO), typeof(XRBaseInteractorEventChannelSO), path) as XRBaseInteractorEventChannelSO;

            #endregion

            #region Events to go in Assets/Resources/Player/Channels/Grab folder

            path = pathGrabFolder;

            grab_LeftHand_SetAttachTransformSO     = CreateSO<VoidEventChannelSO>( nameof(grab_LeftHand_SetAttachTransformSO), typeof(VoidEventChannelSO), path) as VoidEventChannelSO;
            grab_RightHand_SetAttachTransformSO    = CreateSO<VoidEventChannelSO>( nameof(grab_RightHand_SetAttachTransformSO), typeof(VoidEventChannelSO), path) as VoidEventChannelSO;
            grabCountEventChannelSO                = CreateSO<IntEventChannelSO>( nameof(grabCountEventChannelSO), typeof(IntEventChannelSO), path) as IntEventChannelSO;
            grabEvent_LeftHandStateSO              = CreateSO<BoolEventChannelSO>( nameof(grabEvent_LeftHandStateSO), typeof(BoolEventChannelSO), path) as BoolEventChannelSO;
            grabEvent_RightHandStateSO             = CreateSO<BoolEventChannelSO>( nameof(grabEvent_RightHandStateSO), typeof(BoolEventChannelSO), path) as BoolEventChannelSO;
            noHandGrabbingSO                       = CreateSO<VoidEventChannelSO>( nameof(noHandGrabbingSO), typeof(VoidEventChannelSO),  path) as VoidEventChannelSO;
            
            #endregion
        }
        #endif

        private ScriptableObject CreateSO<T>(string name, Type type, string folderPath)
        {
            ScriptableObject so;
            Debug.Log($"Attempting to create so {name}");
            var path = $"Assets/Resources/{folderPath}/{name}.asset";
            // first check if it exists
            var existingSO = Resources.Load<ScriptableObject>(path);
            
            if (existingSO == null)
            {
                Debug.Log($"Did not found channel {name}, creating in folder {folderPath}");
                so = ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(so, path);
            }
            else
            {
                so = existingSO;
                Debug.Log($"The SO: {name} exists.");
            }
            return so;
        }
    }
}