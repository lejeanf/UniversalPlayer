using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace jeanf.vrplayer
{
    public class PoseContainer : MonoBehaviour, IDebugBehaviour
    {
        public bool isDebug
        {
            get => _isDebug;
            set => _isDebug = value;
        }
        [SerializeField] private bool _isDebug = false;
    
        // The pose is when this object is grabbed
        public Pose pose = null;
    
        // The interactor we react to
        private XRGrabInteractable _grabInteractable;

        private void Awake()
        {
            _grabInteractable = GetComponent<XRGrabInteractable>();
        }

        public void SetAttachTransform(HandInfo handInfo)
        {
            if (_grabInteractable == null)
            {
                _grabInteractable = GetComponent<XRGrabInteractable>();
            }
            // in case there is no attatch transform in the Grab Interactable
            if (_grabInteractable.attachTransform == null)
                Instantiate(new GameObject("attachTransform"), _grabInteractable.transform);

            _grabInteractable.attachTransform.localPosition = handInfo.attachPosition;
            _grabInteractable.attachTransform.localRotation = handInfo.attachRotation;

            if (_isDebug) Debug.Log($"attach transform pos: [{handInfo.attachPosition}], rot: [{handInfo.attachRotation.eulerAngles}] ");
        }

        public void SetAttachTransform_Left() => SetAttachTransform(pose.leftHandInfo);
        public void SetAttachTransform_Right()=> SetAttachTransform(pose.rightHandInfo);
    }
}

