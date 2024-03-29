﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SpatialTracking;

public class GrabbableObjectCandidateInfo
{
    public Rigidbody m_Body;
    public List<GameObject> m_Colliders;

    public GrabbableObjectCandidateInfo(Rigidbody body)
    {
        m_Body = body;
        m_Colliders = new List<GameObject>();
    }
}

public class Grabber : MonoBehaviour
{
    public enum PlayerHand
    {
        LEFT,
        RIGHT
    }
    bool m_RefreshGrabAmount = true;

    Rigidbody m_Body;

    LayerMask m_LastObjectLayer;

    [SerializeField]
    Material m_HoverMaterial;

    private static Dictionary<GameObject, GrabbableInformation> m_GrabbableObjectDic;
    public static Dictionary<GameObject, GrabbableInformation> GrabbableObjectsDic
    {
        get
        {
            if (m_GrabbableObjectDic == null)
            {
                m_GrabbableObjectDic = new Dictionary<GameObject, GrabbableInformation>();
            }
            return m_GrabbableObjectDic;
        }
    }

    #region MouseKeyboar Input
    static bool m_UseMouseKeyboardInput = true;
    private static PlayerHand m_CurrentKeyboardMouseHandInput = PlayerHand.RIGHT;
    private Vector3 m_LastMousePos;
    #endregion
    private static Dictionary<GameObject, GrabbableInformation> m_GrabbableUIDic;
    public static Dictionary<GameObject, GrabbableInformation> GrabbableUIDic
    {
        get
        {
            if (m_GrabbableUIDic == null)
            {
                m_GrabbableUIDic = new Dictionary<GameObject, GrabbableInformation>();
            }
            return m_GrabbableUIDic;
        }
    }

    GrabbableInformation m_CurrentGrabbedObject;

    TrackedPoseDriver m_TrackDriver;

    private GameObject m_HandModel;

    [HideInInspector]
    public Animator m_Animator;

    [SerializeField]
    PlayerHand m_PlayerHand;

    public PlayerHand Hand
    {
        get
        {
            return m_PlayerHand;
        }
    }

    List<GrabbableObjectCandidateInfo> m_GrabbableObjectsCandidates;

    new Transform transform;

    Transform m_MainCamera;

    public static void AddGrabbableObjectReference(Grabbable grabbable)
    {
        GrabbableObjectsDic.Add(grabbable.gameObject, new GrabbableInformation(grabbable.GetComponent<Rigidbody>(), grabbable as GrabbableObject));
    }

    public static void RemoveGrabbableObjectReference(Grabbable grabbable)
    {
        GrabbableObjectsDic.Remove(grabbable.gameObject);
    }

    private void Awake()
    {
        SetHandModel();
        m_Body = GetComponent<Rigidbody>();
        m_MainCamera = GameObject.FindGameObjectWithTag("MainCamera").transform;
    }

    // Use this for initialization
    void Start()
    {
        transform = base.transform;

        m_GrabbableObjectsCandidates = new List<GrabbableObjectCandidateInfo>();

        ResizeController.Instance.m_HandsReference.Add(this);
    }

    public void SetHandModel()
    {
        if (m_Animator == null)
        {
            m_Animator = gameObject.GetComponent<Animator>();

            if (m_Animator == null)
            {
                m_Animator = gameObject.GetComponentInChildren<Animator>();
            }

            if (m_Animator != null)
            {
                m_RefreshGrabAmount = ContainsParam("GrabAmount");
            }
        }

        if (m_HandModel != null)
        {
            return;
        }

        SkinnedMeshRenderer rendererObject = GetComponent<SkinnedMeshRenderer>();
        if (rendererObject == null)
        {
            rendererObject = GetComponentInChildren<SkinnedMeshRenderer>();
        }

        if (rendererObject != null)
        {
            m_HandModel = rendererObject.gameObject;
        }
        else
        {
            m_HandModel = gameObject;
        }
    }

    // Update is called once per frame
    void LateUpdate()
    {
        string button = m_PlayerHand == PlayerHand.LEFT ? "LeftTriggerTouch" : "RightTriggerTouch";

        bool mouseClick = m_CurrentKeyboardMouseHandInput == m_PlayerHand && Input.GetMouseButtonDown(0);

        if (m_CurrentGrabbedObject == null && (Input.GetButtonDown(button) || mouseClick))
        {
            HandleGrab();
        }

        if (Input.GetButtonUp(button) || Input.GetMouseButtonUp(0))
        {
            HandleRelease();
        }

        UpdateAnimator();

        UpdateMouseKeyboardInput();

        UpdateGrabbedObject();
	}

    private void UpdateGrabbedObject()
    {
        if(ResizeController.Instance.IsResizing())
        {
            return;
        }

        if(m_CurrentGrabbedObject != null)
        {

            Ray grabRay = new Ray(transform.position, transform.forward);
            RaycastHit hit;

            LayerMask mask = 1 << LayerMask.NameToLayer("wall") | LayerMask.NameToLayer("floor");

            if(Physics.Raycast(grabRay, out hit, 3, mask))
            {
                if (hit.normal.y > 0.5f)
                {
                    m_CurrentGrabbedObject.m_GrabbableObject.transform.forward = Vector3.Cross(m_CurrentGrabbedObject.m_GrabbableObject.transform.right, Vector3.up);
                }

                if (hit.normal.y < 0.5f)
                {
                    m_CurrentGrabbedObject.m_GrabbableObject.transform.forward = hit.normal;
                }
            }
        }
    }

    #region MouseKeyboard Input
    private void UpdateMouseKeyboardInput()
    {
        if(Input.GetKeyDown(KeyCode.M))
        {
            m_UseMouseKeyboardInput = !m_UseMouseKeyboardInput;
        }

        if(!m_UseMouseKeyboardInput)
        {
            return;
        }

        Vector2 mouseDelta = Input.mousePosition - m_LastMousePos;

        if(Input.GetKeyDown(KeyCode.Alpha1))
        {
            m_CurrentKeyboardMouseHandInput = PlayerHand.LEFT;
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            m_CurrentKeyboardMouseHandInput = PlayerHand.RIGHT;
        }

        if (m_CurrentKeyboardMouseHandInput == m_PlayerHand)
        {
            if(Input.GetKey(KeyCode.LeftShift))
            {
                transform.position += m_MainCamera.right * mouseDelta.x * 0.1f;
                transform.position += m_MainCamera.up * mouseDelta.y * 0.1f;
            }
            else
            {
                transform.Rotate(Vector3.up, mouseDelta.x);
                transform.Rotate(Vector3.right, mouseDelta.y);
            }

            if(Input.GetKey(KeyCode.UpArrow))
            {
                transform.position += transform.forward * 0.01f;
            }

            if (Input.GetKey(KeyCode.DownArrow))
            {
                transform.position += transform.forward * -0.01f;
            }

            if (Input.GetKey(KeyCode.RightArrow))
            {
                transform.position += transform.right * 0.01f;
            }

            if (Input.GetKey(KeyCode.LeftArrow))
            {
                transform.position += transform.right * -0.01f;
            }
        }
        m_LastMousePos = Input.mousePosition;
    }
    #endregion

    public void ScaleAround(GameObject target, Vector3 pivot, Vector3 newScale)
    {
        Vector3 A = target.transform.localPosition;
        Vector3 B = pivot;

        Vector3 C = A - B; // diff from object pivot to desired pivot/origin

        float RS = newScale.x / target.transform.localScale.x; // relative scale factor

        // calc final position post-scale
        Vector3 FP = B + C * RS;

        // finally, actually perform the scale/translation
        target.transform.localScale = newScale;
        target.transform.localPosition = FP;
    }

    public bool ContainsParam(string parameter)
    {
        foreach (AnimatorControllerParameter param in m_Animator.parameters)
        {
            if (param.name == parameter)
            {
                return true;
            }
        }
        return false;
    }

    private void UpdateAnimator()
    {
        if(!m_RefreshGrabAmount)
        {
            return;
        }

        string axis = m_PlayerHand == PlayerHand.LEFT ? "LeftTriggerSqueeze" : "RightTriggerSqueeze";
        m_Animator.SetFloat("GrabAmount", Input.GetAxis(axis));
    }

    void HandleGrab()
    {
        GameObject grabbable = GetClosestGrabbableObject();

        if(grabbable != null)
        {
            GrabbableInformation grabbableObj;

            if (!GrabbableObjectsDic.TryGetValue(grabbable, out grabbableObj))
            {
                return;
            }

            if (grabbableObj.m_GrabbableObject.CurrentGrabber != null)
            {
                ResizeController.Instance.EnterResizeMode(grabbableObj.m_GrabbableObject.gameObject, this);
            }
            else
            {
                grabbableObj.m_GrabbableObject.ObjectGrabbed(this);
                m_CurrentGrabbedObject = grabbableObj;
                m_CurrentGrabbedObject.m_GrabbableObject.RestoreMat();
                grabbableObj.m_GrabbableObject.transform.parent = transform;
                grabbableObj.m_Body.isKinematic = true;
                grabbableObj.m_Body.useGravity = false;
                m_LastObjectLayer = grabbableObj.m_GrabbableObject.gameObject.layer;
                SetLayerToObject(grabbableObj.m_GrabbableObject.gameObject, LayerMask.NameToLayer("Moving"));
            }

            m_HandModel.SetActive(false);
        }
    }

    void HandleRelease()
    {
        if (m_CurrentGrabbedObject != null)
        {
            m_CurrentGrabbedObject.m_GrabbableObject.ObjectReleased();

            m_CurrentGrabbedObject.m_GrabbableObject.SetHoverMat(m_HoverMaterial);

            if(ResizeController.Instance.m_ResizingHand != null)
            {
                Grabber otherHand = ResizeController.Instance.m_ResizingHand;
                ResizeController.Instance.ExitResizeMode();
                otherHand.HandleGrab();
            }
        }
        else if(ResizeController.Instance.m_ResizingHand == this)
        {
            ResizeController.Instance.ExitResizeMode();
        }

        if(m_CurrentGrabbedObject != null)
        {
            m_CurrentGrabbedObject.m_GrabbableObject.transform.parent = null;
            m_CurrentGrabbedObject.m_Body.isKinematic = m_CurrentGrabbedObject.KinematicSetup;
            m_CurrentGrabbedObject.m_Body.useGravity = m_CurrentGrabbedObject.GravitySetup;

            m_CurrentGrabbedObject.m_Body.velocity = m_Body.velocity;
            m_CurrentGrabbedObject.m_Body.angularVelocity = m_Body.angularVelocity;

            SetLayerToObject( m_CurrentGrabbedObject.m_GrabbableObject.gameObject, m_LastObjectLayer);
        }

        m_CurrentGrabbedObject = null;
        m_HandModel.SetActive(true);

    }

    public void SetGrabberVisibility(bool visible)
    {
        m_HandModel.SetActive(visible);
    }

    GameObject GetClosestGrabbableObject()
    {
        float closestDistance = float.PositiveInfinity;

        GameObject closestGrabbable = null;

        for(int i = 0; i < m_GrabbableObjectsCandidates.Count; i++)
        {
            float dist = (transform.position - m_GrabbableObjectsCandidates[i].m_Body.transform.position).sqrMagnitude;

            if(dist < closestDistance)
            {
                closestDistance = dist;
                closestGrabbable = m_GrabbableObjectsCandidates[i].m_Body.gameObject;
            }
        }

        return closestGrabbable;
    } 

    void SetLayerToObject(GameObject obj, LayerMask layer)
    {
        obj.layer = layer;

        Collider[] cols = obj.GetComponentsInChildren<Collider>();

        foreach(Collider col in cols)
        {
            col.gameObject.layer = layer;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        GrabbableInformation grabbable;
        if(other.attachedRigidbody != null && GrabbableObjectsDic.TryGetValue(other.attachedRigidbody.gameObject, out grabbable))
        {
            GrabbableObjectCandidateInfo candidateInfo = m_GrabbableObjectsCandidates.Find(Item => Item.m_Body == other.attachedRigidbody);

            if(candidateInfo == null)
            {
                candidateInfo = new GrabbableObjectCandidateInfo(other.attachedRigidbody);
                m_GrabbableObjectsCandidates.Add(candidateInfo);
            }

            if (!candidateInfo.m_Colliders.Contains(other.gameObject))
            {
                candidateInfo.m_Colliders.Add(other.gameObject);
            }

            if(candidateInfo.m_Colliders.Count == 1)
            {
                other.attachedRigidbody.gameObject.GetComponent<GrabbableObject>().SetHoverMat(m_HoverMaterial);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if(other.attachedRigidbody == null)
        {
            return;
        }

        GrabbableObjectCandidateInfo candidateInfo = m_GrabbableObjectsCandidates.Find(Item => Item.m_Body == other.attachedRigidbody);

        if(candidateInfo != null && candidateInfo.m_Colliders.Contains(other.gameObject))
        {
            candidateInfo.m_Colliders.Remove(other.gameObject);

            if(candidateInfo.m_Colliders.Count == 0)
            {
                other.attachedRigidbody.gameObject.GetComponent<GrabbableObject>().RestoreMat();
            }
        }
    }
}
