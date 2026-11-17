using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 흐린 배경 (UIBlurBackground, 기획서 3-3 ⑨) EditMode 확인.
/// - 같은 입력이면 다시 처리하지 않는다
/// - RenderTexture는 처음 만든 3장(결과 2 + 중간 1)을 계속 다시 쓴다
/// - 처음·instant는 페이드 없이 앞 RawImage만, 입력이 바뀌면 앞·뒤를 섞는다 (앞 = 새 결과, 뒤 = 이전 결과)
/// - 16:9가 아닌 입력은 가운데를 잘라 맞춘다 (GetCrop)
/// </summary>
public class UIBlurBackgroundTests
{
    private GameObject root;
    private Texture2D textureA;
    private Texture2D textureB;

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
        if (textureA != null) Object.DestroyImmediate(textureA);
        if (textureB != null) Object.DestroyImmediate(textureB);
    }

    private UIBlurBackground Create()
    {
        root = new GameObject("Background", typeof(RectTransform));
        GameObject back = new GameObject("Img_BackgroundBack", typeof(RectTransform), typeof(RawImage));
        back.transform.SetParent(root.transform, false);
        GameObject front = new GameObject("Img_BackgroundFront", typeof(RectTransform), typeof(RawImage));
        front.transform.SetParent(root.transform, false);

        UIBlurBackground blur = front.AddComponent<UIBlurBackground>();
        SerializedObject so = new SerializedObject(blur);
        so.FindProperty("blurMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/UIBlur.mat");
        so.FindProperty("backImage").objectReferenceValue = back.GetComponent<RawImage>();
        so.ApplyModifiedPropertiesWithoutUndo();

        textureA = new Texture2D(64, 36);
        textureB = new Texture2D(32, 32);
        return blur;
    }

    [Test]
    public void SameSource_NotProcessedAgain_TexturesReused()
    {
        UIBlurBackground blur = Create();
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/UIBlur.mat"), "블러 머티리얼 에셋");

        blur.SetSource(textureA, true);
        Assert.AreEqual(1, blur.ProcessCount);
        Assert.AreEqual(3, blur.CreatedTextureCount, "결과 2장 + 중간 1장");
        Assert.IsTrue(blur.FrontImage.texture is RenderTexture);
        Assert.AreEqual(new Vector2Int(480, 270), blur.ResultSize, "1920×1080의 1/4");
        Assert.AreEqual(480, blur.FrontImage.texture.width);
        Assert.AreEqual(1f, blur.FrontImage.color.a, 1e-4f, "처음은 페이드 없이 바로");
        Assert.IsFalse(blur.BackImage.enabled);

        blur.SetSource(textureA, false);
        blur.SetSource(textureA, true);
        Assert.AreEqual(1, blur.ProcessCount, "같은 입력은 다시 처리하지 않음");

        Texture firstResult = blur.FrontImage.texture;
        blur.SetSource(textureB, false);
        Assert.AreEqual(2, blur.ProcessCount);
        Assert.AreEqual(3, blur.CreatedTextureCount, "RenderTexture를 새로 만들지 않음");
        Assert.IsTrue(blur.IsFading, "입력이 바뀌면 크로스페이드");
        Assert.AreNotSame(firstResult, blur.FrontImage.texture, "새 결과는 다른 한 장에");
        Assert.AreSame(firstResult, blur.BackImage.texture, "뒤에는 이전 결과");
        Assert.IsTrue(blur.BackImage.enabled);
        Assert.AreEqual(0f, blur.FrontImage.color.a, 1e-4f, "새 결과는 0에서 시작");

        blur.SetSource(textureA, true);
        Assert.AreEqual(3, blur.ProcessCount);
        Assert.AreEqual(3, blur.CreatedTextureCount, "번갈아 다시 씀");
        Assert.AreSame(firstResult, blur.FrontImage.texture, "결과 2장을 번갈아 쓴다");
        Assert.IsFalse(blur.IsFading, "instant는 페이드 없음");
        Assert.IsFalse(blur.BackImage.enabled);
    }

    [Test]
    public void GetCrop_CentersToTargetAspect()
    {
        float target = 16f / 9f;
        UIBlurBackground.GetCrop(target, target, out Vector2 scale, out Vector2 offset);
        Assert.AreEqual(Vector2.one, scale);
        Assert.AreEqual(Vector2.zero, offset);

        // 정사각 앨범: 세로를 9/16만 쓰고 가운데
        UIBlurBackground.GetCrop(1f, target, out scale, out offset);
        Assert.AreEqual(1f, scale.x, 1e-5f);
        Assert.AreEqual(0.5625f, scale.y, 1e-5f);
        Assert.AreEqual(0.21875f, offset.y, 1e-5f);

        // 더 넓은 입력: 가로를 잘라 가운데
        UIBlurBackground.GetCrop(32f / 9f, target, out scale, out offset);
        Assert.AreEqual(0.5f, scale.x, 1e-5f);
        Assert.AreEqual(0.25f, offset.x, 1e-5f);
        Assert.AreEqual(1f, scale.y, 1e-5f);
    }

    [Test]
    public void NullSource_ShowsBlack_NoProcess()
    {
        UIBlurBackground blur = Create();
        blur.SetSource((Texture)null, true);
        Assert.AreEqual(0, blur.ProcessCount);
        Assert.AreEqual(0, blur.CreatedTextureCount);
        Assert.IsNull(blur.FrontImage.texture);
    }
}
