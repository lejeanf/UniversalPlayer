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

        [Header("Left attach transform")]
        [SerializeField] private Vector3 leftAttachPosition;
        [SerializeField] private Quaternion leftAttachRotation;
        
        [Header("Right attach transform")]
        [SerializeField] private Vector3 rightAttachPosition;
        [SerializeField] private Quaternion rightAttachRotation;
        private void Awake()
        {
            _grabInteractable = GetComponent<XRGrabInteractable>();
        }
        private void OnEnable()
        {
            TakeObject.OnHandGrabbed += SetAttachTransform;
        }

        private void SetAttachTransform(HandType handSide)
        {
            switch (handSide)
            {
                case HandType.Left:
                    _grabInteractable.attachTransform.position = leftAttachPosition;
                    _grabInteractable.attachTransform.rotation = leftAttachRotation;
                    break;
                case HandType.Right:
                    _grabInteractable.attachTransform.position = rightAttachPosition;
                    _grabInteractable.attachTransform.rotation = rightAttachRotation;
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

        //public void SetAttachTransform_Left() => SetAttachTransform(pose.leftHandInfo);
        //public void SetAttachTransform_Right()=> SetAttachTransform(pose.rightHandInfo);
    }
}

