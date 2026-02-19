using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.Events;

[System.Flags]
public enum SafeAreaBorder { None = 0, Left = 1, Top = 2, Right = 4, Bottom = 8 }
public enum SafeAreaEvent { Awake, Enable, Update }

[System.Serializable]
public class UnityRectEvent : UnityEvent<RectOffset> { }

public class SafeArea : MonoBehaviour {
    #region Simulations
    /// <summary>
    /// Simulation device that uses safe area due to a physical notch or software home bar. For use in Editor only.
    /// </summary>
    public enum SimDevice {
        /// <summary>
        /// Don't use a simulated safe area - GUI will be full screen as normal.
        /// </summary>
        None,
        /// <summary>
        /// Simulate the iPhone X and Xs (identical safe areas).
        /// </summary>
        iPhoneX,
        /// <summary>
        /// Simulate the iPhone Xs Max and XR (identical safe areas).
        /// </summary>
        iPhoneXsMax,
        /// <summary>
        /// Simulate the Google Pixel 3 XL using landscape left.
        /// </summary>
        Pixel3XL_LSL,
        /// <summary>
        /// Simulate the Google Pixel 3 XL using landscape right.
        /// </summary>
        Pixel3XL_LSR
    }

    /// <summary>
    /// Simulation mode for use in editor only. This can be edited at runtime to toggle between different safe areas.
    /// </summary>
    public static SimDevice Sim = SimDevice.None;

    /// <summary>
    /// Normalised safe areas for iPhone X with Home indicator (ratios are identical to Xs, 11 Pro). Absolute values:
    ///  PortraitU x=0, y=102, w=1125, h=2202 on full extents w=1125, h=2436;
    ///  PortraitD x=0, y=102, w=1125, h=2202 on full extents w=1125, h=2436 (not supported, remains in Portrait Up);
    ///  LandscapeL x=132, y=63, w=2172, h=1062 on full extents w=2436, h=1125;
    ///  LandscapeR x=132, y=63, w=2172, h=1062 on full extents w=2436, h=1125.
    ///  Aspect Ratio: ~19.5:9.
    /// </summary>
    Rect[] NSA_iPhoneX = new Rect[] {
        new Rect (0f, 102f / 2436f, 1f, 2202f / 2436f),  // Portrait
        new Rect (132f / 2436f, 63f / 1125f, 2172f / 2436f, 1062f / 1125f)  // Landscape
    };

    /// <summary>
    /// Normalised safe areas for iPhone Xs Max with Home indicator (ratios are identical to XR, 11, 11 Pro Max). Absolute values:
    ///  PortraitU x=0, y=102, w=1242, h=2454 on full extents w=1242, h=2688;
    ///  PortraitD x=0, y=102, w=1242, h=2454 on full extents w=1242, h=2688 (not supported, remains in Portrait Up);
    ///  LandscapeL x=132, y=63, w=2424, h=1179 on full extents w=2688, h=1242;
    ///  LandscapeR x=132, y=63, w=2424, h=1179 on full extents w=2688, h=1242.
    ///  Aspect Ratio: ~19.5:9.
    /// </summary>
    Rect[] NSA_iPhoneXsMax = new Rect[] {
        new Rect (0f, 102f / 2688f, 1f, 2454f / 2688f),  // Portrait
        new Rect (132f / 2688f, 63f / 1242f, 2424f / 2688f, 1179f / 1242f)  // Landscape
    };

    /// <summary>
    /// Normalised safe areas for Pixel 3 XL using landscape left. Absolute values:
    ///  PortraitU x=0, y=0, w=1440, h=2789 on full extents w=1440, h=2960;
    ///  PortraitD x=0, y=0, w=1440, h=2789 on full extents w=1440, h=2960;
    ///  LandscapeL x=171, y=0, w=2789, h=1440 on full extents w=2960, h=1440;
    ///  LandscapeR x=0, y=0, w=2789, h=1440 on full extents w=2960, h=1440.
    ///  Aspect Ratio: 18.5:9.
    /// </summary>
    Rect[] NSA_Pixel3XL_LSL = new Rect[] {
        new Rect (0f, 0f, 1f, 2789f / 2960f),  // Portrait
        new Rect (0f, 0f, 2789f / 2960f, 1f)  // Landscape
    };

    /// <summary>
    /// Normalised safe areas for Pixel 3 XL using landscape right. Absolute values and aspect ratio same as above.
    /// </summary>
    Rect[] NSA_Pixel3XL_LSR = new Rect[]  {
        new Rect (0f, 0f, 1f, 2789f / 2960f),  // Portrait
        new Rect (171f / 2960f, 0f, 2789f / 2960f, 1f)  // Landscape
    };
    #endregion

    RectTransform _panel;
    Rect _lastSafeArea = new Rect(0, 0, 0, 0);
    Vector2 _initialOffsetMin = Vector2.zero;
    Vector2 _initialOffsetMax = Vector2.zero;
    Vector2Int _lastScreenSize = new Vector2Int (0, 0);
    ScreenOrientation _lastOrientation = ScreenOrientation.AutoRotation;

    [Header("Apply options")]
    [SerializeField] bool applySafeArea = true;
    [MMCondition("applySafeArea", true)]
    [SerializeField] SafeAreaBorder applyTo;
    [MMCondition("applySafeArea", true)]
    [SerializeField] bool useTransformValueIfZero = true;
    [MMCondition("applySafeArea", true)]
    [SerializeField] bool logging = false;

    //[Header("Refresh options")]
    //[SerializeField] SafeAreaEvent refreshEvent = SafeAreaEvent.Enable;
    //[MMEnumCondition("refreshEvent", 2)]
    //[SerializeField] bool oneTime = true;
    //[SerializeField] bool forceRefresh = false;

    [Header("Events")]
    [SerializeField] UnityRectEvent OnRefresh;

    bool _triggered;

    void Awake() {
        _panel = GetComponent<RectTransform>();

        if (_panel == null) {
            Debug.LogError ("Cannot apply safe area - no RectTransform found on " + name);
            Destroy (gameObject);
        }

        _initialOffsetMin = _panel.offsetMin;
        _initialOffsetMax = _panel.offsetMax;

        //if (refreshEvent == SafeAreaEvent.Awake)
        //    Refresh();
    }

    void Update(){
        if (/*refreshEvent == SafeAreaEvent.Update && (!oneTime ||*/ !_triggered)
            Refresh();
    }

    /*void OnEnable() {
        if (refreshEvent == SafeAreaEvent.Enable)
            Refresh();
    }*/

    void Refresh() {
        Rect safeArea = GetSafeArea ();

        /*if (forceRefresh || safeArea != _lastSafeArea
            || Screen.width != _lastScreenSize.x
            || Screen.height != _lastScreenSize.y
            || Screen.orientation != _lastOrientation) {
            // Fix for having auto-rotate off and manually forcing a screen orientation.*/
            // See https://forum.unity.com/threads/569236/#post-4473253 and https://forum.unity.com/threads/569236/page-2#post-5166467
            _lastScreenSize.x = Screen.width;
            _lastScreenSize.y = Screen.height;
            _lastOrientation = Screen.orientation;

            ApplySafeArea(safeArea);
        //}

        if (logging)
            Debug.LogFormat("Safe area checked for {0}:", gameObject.name);
        _triggered = true;
    }

    Rect GetSafeArea() {
        Rect safeArea = Screen.safeArea;

        if (Application.isEditor && Sim != SimDevice.None) {
            Rect nsa = new Rect(0, 0, Screen.width, Screen.height);

            switch (Sim) {
                case SimDevice.iPhoneX:
                    if (Screen.height > Screen.width)  // Portrait
                        nsa = NSA_iPhoneX[0];
                    else  // Landscape
                        nsa = NSA_iPhoneX[1];
                    break;
                case SimDevice.iPhoneXsMax:
                    if (Screen.height > Screen.width)  // Portrait
                        nsa = NSA_iPhoneXsMax[0];
                    else  // Landscape
                        nsa = NSA_iPhoneXsMax[1];
                    break;
                case SimDevice.Pixel3XL_LSL:
                    if (Screen.height > Screen.width)  // Portrait
                        nsa = NSA_Pixel3XL_LSL[0];
                    else  // Landscape
                        nsa = NSA_Pixel3XL_LSL[1];
                    break;
                case SimDevice.Pixel3XL_LSR:
                    if (Screen.height > Screen.width)  // Portrait
                        nsa = NSA_Pixel3XL_LSR[0];
                    else  // Landscape
                        nsa = NSA_Pixel3XL_LSR[1];
                    break;
                default:
                    break;
            }

            safeArea = new Rect(Screen.width * nsa.x, Screen.height * nsa.y, Screen.width * nsa.width, Screen.height * nsa.height);
        }

        return safeArea;
    }

    void ApplySafeArea(Rect r) {
        _lastSafeArea = r;

        // Check for invalid screen startup state on some Samsung devices (see below)
        if (Screen.width > 0 && Screen.height > 0) {

            RectOffset delta = new RectOffset();
            delta.left = (int)r.x;
            delta.top = Screen.height - (int)r.yMax;
            delta.right = Screen.width - (int)r.xMax;
            delta.bottom = (int)r.y;

            if (applySafeArea) {
                Vector2 offsetMin, offsetMax;
                offsetMin.x = applyTo.HasFlag(SafeAreaBorder.Left) && (delta.left > 0 || !useTransformValueIfZero) ? delta.left : _initialOffsetMin.x;
                offsetMin.y = applyTo.HasFlag(SafeAreaBorder.Bottom) && (delta.bottom > 0 || !useTransformValueIfZero) ? delta.bottom : _initialOffsetMin.y;
                offsetMax.x = applyTo.HasFlag(SafeAreaBorder.Right) && (delta.right > 0 || !useTransformValueIfZero) ? -delta.right : _initialOffsetMax.x;
                offsetMax.y = applyTo.HasFlag(SafeAreaBorder.Top) && (delta.top > 0 || !useTransformValueIfZero) ? -delta.top : _initialOffsetMax.y;

                _panel.offsetMin = offsetMin;
                _panel.offsetMax = offsetMax;
            }

            OnRefresh?.Invoke(delta);
        }

        if (applySafeArea && logging) {
            Debug.LogFormat("New safe area applied to {0}: x={1}, y={2}, w={3}, h={4} on full extents w={5}, h={6}",
            gameObject.name, r.x, r.y, r.width, r.height, Screen.width, Screen.height);
        }
    }
}
