using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

/// <summary>
/// 팝업 뒤 화면 (기획서 3-7 「팝업 공통 톤 (11/18)」). 세 팝업(설정 창 · 확인 팝업 · 일시정지)이 같이 쓰는 공용 컴포넌트로, 각 팝업 맨 아래 PopupBackdrop 오브젝트에 붙인다.
/// - 구조: PopupBackdrop(이 스크립트) → Img_Blur(RawImage + UIBlurBackground, 클릭 안 받음) → Img_Dim(덮개 rgba(36,32,46) 42%, 클릭을 막는다 = UIPopup의 dimBackground).
/// - 팝업이 열릴 때(UIPopup.Open) Capture()를 부른다: 그 프레임 끝에 지금 화면을 RenderTexture로 한 번 캡처하고(ScreenCapture) 블러 6px로 흐린다.
///   채도·밝기 보정은 하지 않는다 (100%). 열리는 첫 프레임은 팝업이 페이드 0이라 팝업 자신은 캡처에 들어가지 않는다.
/// - 팝업 위에 팝업이 열리면 위 팝업이 자기 Capture로 아래 팝업까지 다시 캡처한다. 위 팝업을 닫으면 아래 팝업은 자기 결과 그대로 보인다.
/// - 캡처용 RenderTexture는 화면 크기로 한 번 만들어 다시 쓰고(화면 크기가 바뀌면 다시 만든다), OnDestroy에서 해제한다. 블러 결과 텍스처는 UIBlurBackground가 다시 쓴다.
/// - 플레이 중 프레임에 영향이 없게 열 때 한 번만 처리한다 (매 프레임 처리하지 않는다).
/// - 색은 UITheme 팝업 묶음(Img_Dim은 UIThemeColor 역할 PopupDim)으로 칠한다.
/// </summary>
public class PopupBackdrop : MonoBehaviour
{
    private const string LogPrefix = "[PopupBackdrop]";

    [Tooltip("뒤 화면 블러 (Img_Blur, RawImage + UIBlurBackground)")]
    [SerializeField] private UIBlurBackground blur;
    [Tooltip("블러 위 덮개 (Img_Dim, 클릭을 막는다)")]
    [SerializeField] private Image dim;

    private RenderTexture capture;
    private Coroutine captureRoutine;

    /// <summary>블러 (확인용)</summary>
    public UIBlurBackground Blur => blur;

    /// <summary>덮개 (확인용)</summary>
    public Image Dim => dim;

    /// <summary>블러 결과가 보이는 중이면 true (확인용)</summary>
    public bool HasBlurTexture => blur != null && blur.FrontImage.texture != null && blur.FrontImage.color.a > 0f;

    /// <summary>캡처한 횟수 (확인용)</summary>
    public int CaptureCount { get; private set; }

    /// <summary>마지막 캡처 + 블러에 걸린 CPU 시간 (ms, GPU 명령을 넣는 데 걸린 시간)</summary>
    public double LastCaptureMilliseconds { get; private set; }

    /// <summary>캡처를 기다리는 중이면 true (확인용)</summary>
    public bool IsCapturing => captureRoutine != null;

    /// <summary>
    /// 덮개 색을 칠하고 블러 세기·색 보정(없음)을 정한다. 팝업 Init에서 부른다.
    /// </summary>
    public void Init()
    {
        UIThemeColor.ApplyAll(gameObject);
        if (blur != null)
        {
            blur.SetColorGrade(1f, 1f);
            blur.SetBlurRadiusPx(GameConstants.Popup.BlurRadiusPx, GameConstants.Popup.BlurIterations);
            blur.HideResult();
        }
    }

    /// <summary>
    /// 이 프레임 끝에 지금 화면을 캡처해 흐린다 (팝업이 열릴 때 한 번). 오브젝트가 꺼져 있으면 아무것도 하지 않는다.
    /// </summary>
    public void Capture()
    {
        if (blur == null)
        {
            Debug.LogWarning($"{LogPrefix} blur가 연결되지 않아 뒤 화면을 흐리지 않습니다.", this);
            return;
        }
        if (!isActiveAndEnabled) return;

        if (captureRoutine != null) StopCoroutine(captureRoutine);
        blur.HideResult();
        captureRoutine = StartCoroutine(CaptureAtEndOfFrame());
    }

    private IEnumerator CaptureAtEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        captureRoutine = null;

        int width = Mathf.Max(1, Screen.width);
        int height = Mathf.Max(1, Screen.height);
        Stopwatch watch = Stopwatch.StartNew();

        EnsureCapture(width, height);
        ScreenCapture.CaptureScreenshotIntoRenderTexture(capture);
        // D3D·Metal 등 텍스처 좌표가 위에서 시작하는 API는 캡처가 위아래로 뒤집혀 온다
        blur.ProcessNow(capture, SystemInfo.graphicsUVStartsAtTop);

        watch.Stop();
        LastCaptureMilliseconds = watch.Elapsed.TotalMilliseconds;
        CaptureCount++;
    }

    /// <summary>화면 크기 캡처 텍스처를 한 번 만든다 (크기가 바뀌면 다시 만든다)</summary>
    private void EnsureCapture(int width, int height)
    {
        if (capture != null && capture.width == width && capture.height == height && capture.IsCreated()) return;

        Release();
        capture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
        {
            name = $"{name}_ScreenCapture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false
        };
        capture.Create();
    }

    private void OnDisable()
    {
        captureRoutine = null;
    }

    private void OnDestroy()
    {
        Release();
    }

    private void Release()
    {
        if (capture == null) return;
        capture.Release();
        if (Application.isPlaying) Destroy(capture);
        else DestroyImmediate(capture);
        capture = null;
    }
}
