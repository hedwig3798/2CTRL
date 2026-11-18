using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 바닥 타일 경계 틈 방지
/// - 타일은 타일마다 따로 그리는 사각형이라 경계가 화면 픽셀 중앙에 딱 걸리면 배경색 1px 줄이 생긴다
/// - WFCChunkLoader.seamOverlap 만큼 타일을 크게 그려 이웃과 살짝 겹치게 한다 (0 보다 크고, 그림이 밀려 보이지 않을 만큼 작게)
/// </summary>
public class TileSeamTests
{
    [TestCase("Assets/prefab/Managers/ChunkLoader_D1.prefab")]
    [TestCase("Assets/prefab/Managers/ChunkLoader_D2.prefab")]
    [TestCase("Assets/prefab/Managers/ChunkLoader_D3.prefab")]
    public void ChunkLoader_HasSubPixelSeamOverlap(string _path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_path);
        Assert.NotNull(prefab, _path);
        WFCChunkLoader loader = prefab.GetComponent<WFCChunkLoader>();
        Assert.NotNull(loader, _path);

        Assert.Greater(loader.seamOverlap, 0f, "겹침이 없으면 타일 경계에 틈이 생긴다");
        Assert.Less(loader.seamOverlap, 0.01f, "너무 크면 타일 그림이 이웃 칸으로 밀려 보인다");
    }
}
