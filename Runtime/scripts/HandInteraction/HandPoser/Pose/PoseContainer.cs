using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
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

        [Header("Left attach transform")]
        [SerializeField] private Vector3 leftAttachPosition;
        [SerializeField] private Vector3 leftAttachRotation;
        
        [Header("Right attach transform")]
        [SerializeField] private Vector3 rightAttachPosition;
        [SerializeField] private Vector3 rightAttachRotation;

        [Header("Listening On")]
        VoidEventChannelSO leftHandHovered;
        VoidEventChannelSO rightHandHovered;
        private void Awake()
        {
            _grabInteractable = GetComponent<XRGrabInteractable>();
        }

        public void SetAttachTransform_Left() => SetAttachTransform(HandType.Left);
        public void SetAttachTransform_Right() => SetAttachTransform(HandType.Right);
        public void SetAttachTransform(HandType handSide)
        {
            switch (handSide)
            {
                case HandType.Left:
                    _grabInteractable.attachTransform.localPosition = leftAttachPosition;
                    _grabInteractable.attachTransform.localEulerAngles = leftAttachRotation;
                    break;
                case HandType.Right:
                    _grabInteractable.attachTransform.localPosition = rightAttachPosition;
                    _grabInteractable.attachTransform.localEulerAngles = rightAttachRotation;
                    break;
            }
        }



        //public void SetAttachTransform(HandInfo handInfo)
        //{
        //    //Debug.Log("SetAttachTransform");
        //    //if (_grabInteractable == null)
        //    //{
        //    //    _grabInteractable = GetComponent<XRGrabInteractable>();
        //    //}
        //    //// in case there is no attatch transform in the Grab Interactable
        //    //if (_grabInteractable.attachTransform == null)
        //    //    Instantiate(new GameObject("attachTransform"), _grabInteractable.transform);

        //    //_grabInteractable.attachTransform.position = handInfo.attachPosition;
        //    //_grabInteractable.attachTransform.rotation = handInfo.attachRotation;

        //    //if (_isDebug) Debug.Log($"attach transform pos: [{handInfo.attachPosition}], rot: [{handInfo.attachRotation.eulerAngles}] ");
        //}

    }
}

