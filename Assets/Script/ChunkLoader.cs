using System.Collections.Generic;
using System.Drawing;
using TMPro;
using UnityEngine;
using UnityEngine.U2D;
using static UnityEngine.UI.Image;

public class ChunkedBackground : MonoBehaviour
{
    [Header("Target")]
    // 청크가 생성될 기준
    [SerializeField] private Transform target;

    // 타일이 모여 청크, 청크가 모여 로드영역이 된다.
    [Header("Chunk Setting")]
    // 전체 로드 영역 ( 1 = 1x1, 2 = 3x3 )
    [SerializeField] private int totalRadius = 1;
    // 한 청크의 크기 ( N x N )
    [SerializeField] private int chunkCount = 1;
    // 한 타일의 크기 ( 타일 사이즈가 0 이라면 이미지 크기가 된다. )
    [SerializeField] private Vector2Int tileSize = Vector2Int.one;

    [Header("Sprite")]
    // 스프라이트
    [SerializeField] private Sprite[] backgroundSprite;

    [Header("Rendering")]
    // 렌더 레이어
    [SerializeField] private string sortingLayerName = "Background";

    [Header("Object Layer")]
    // 오브젝트 레이어
    [SerializeField] private string objectLayerName = "Default";

    // 청크 데이터 최적화
    private readonly Dictionary<Vector2Int, GameObject> _chunkPool = new();
    private readonly Dictionary<Vector2Int, GameObject> _activeChunk = new();

    // 모든 청크의 루트
    private Transform _root;

    private Vector2Int _tileSize = Vector2Int.one;
    private Vector2Int _chunkSize = Vector2Int.one;

    private Vector2Int _lastChunkIndex;


    // 예외처리
    void Awake()
    {
        if (null == target)
        {
            if (null == Camera.main)
            {
                Debug.LogError("[ChunkedBackground] 추적할 카메라가 없습니다.");
                enabled = false;
                return;
            }

            target = Camera.main.transform;
        }

        if (0 == backgroundSprite.Length)
        {
            Debug.LogError("[ChunkedBackground] 지정된 이미지가 없습니다.");
            enabled = false;
            return;
        }

        // 청크 루트
        _root = new GameObject("ChunkRoot").transform;
        _root.SetParent(transform, false);

        // 이미지 크기
        if (Vector2.zero == tileSize)
        {
            Vector2 spriteSize = backgroundSprite[0].bounds.size;
            _tileSize = new Vector2Int((int)spriteSize.x, (int)spriteSize.y);
        }
        else
        {
            _tileSize = tileSize;
        }

        _chunkSize = _tileSize * chunkCount;

        // 첫 청크의 위치 파악 및 강제 업데이트
        _lastChunkIndex = GetChunkIndex(target.position);
        UpdateChunks();
    }

    // 카메라가 이동을 한 후 청크 로딩이 돼어야한다.
    void LateUpdate()
    {
        // 청크 루트가 있어야 할 위치를 가져온다.
        Vector2Int cur = GetChunkIndex(target.position);

        // 이전과 지금의 청크 루트가 다르다면 로드한다.
        if (cur != _lastChunkIndex)
        {
            _lastChunkIndex = cur;
            UpdateChunks();
        }
    }

    // 월드값에서 현재 위치하고 있는 청크의 인덱스를 구한다.
    private Vector2Int GetChunkIndex(Vector3 worldPos)
    {
        // 중심 기준 오프셋 보정
        float halfX = _chunkSize.x / 2f;
        float halfY = _chunkSize.y / 2f;

        // 중심 정렬된 좌표계에서의 인덱스 계산
        int cx = Mathf.FloorToInt((worldPos.x + halfX) / _chunkSize.x);
        int cy = Mathf.FloorToInt((worldPos.y + halfY) / _chunkSize.y);

        return new Vector2Int(cx, cy);
    }

    // 청크 업데이트
    private void UpdateChunks()
    {
        // 새로 생성할 청크
        HashSet<Vector2Int> needChunk = new();
        for (int y = -totalRadius; y <= totalRadius; y++)
        {
            for (int x = -totalRadius; x <= totalRadius; x++)
            {
                // 새로 생성할 청크의 좌표값을 넣는다.
                needChunk.Add(_lastChunkIndex + new Vector2Int(x, y));
            }
        }

        // 반환 해야하는 청크
        List<Vector2Int> removeChunk = new();
        foreach (var kv in _activeChunk)
        {
            if (false == needChunk.Contains(kv.Key))
            {
                removeChunk.Add(kv.Key);
            }
        }

        // 청크 반환
        foreach (var key in removeChunk)
        {
            var chunk = _activeChunk[key];
            _activeChunk.Remove(key);
            ReturnToPool(chunk);
        }

        // 청크 생성
        foreach (var key in needChunk)
        {
            if (true == _activeChunk.ContainsKey(key))
            {
                continue;
            }
            else
            {
                var chunk = GetFromPoolOrCreate(key);
                _activeChunk[key] = chunk;
            }
        }
    }

    // 청크 풀에서 청크 생성 혹은 가져오기
    private GameObject GetFromPoolOrCreate(Vector2Int key)
    {
        // 풀에 청크가 있다면 가져온다. 없으면 생성한다.
        if (_chunkPool.ContainsKey(key))
        {
            _chunkPool[key].SetActive(true);
            return _chunkPool[key];
        }

        GameObject go = CreateChunkGO(key);
        go.SetActive(true);
        return go;
    }

    // 청크 풀에 청크 반환
    private void ReturnToPool(GameObject go)
    {
        // 청크 활성화 끄고 큐에 넣는다.
        go.SetActive(false);
    }

    // 청크 생성
    private GameObject CreateChunkGO(Vector2Int key)
    {
        // 청크 오브젝트 생성
        var chunk = new GameObject($"Chunk_{key.x}_{key.y}");

        // 루트를 부모로
        chunk.transform.SetParent(_root, false);

        // 청크의 중심 좌표
        float cx = key.x * _chunkSize.x;
        float cy = key.y * _chunkSize.y;
        chunk.transform.position = new Vector3(cx, cy, 0);

        // 청크 좌상단 좌표 (청크 중심에서 절반 이동)
        float originX = -_chunkSize.x / 2f;
        float originY = _chunkSize.y / 2f;

        for (int i = 0; i < chunkCount; i++)
        {
            for (int j = 0; j < chunkCount; j++)
            {
                var tile = new GameObject($"Tile_{i}_{j}");
                tile.transform.SetParent(chunk.transform, false);

                // 스프라이트 추가
                var sr = tile.AddComponent<SpriteRenderer>();
                sr.sprite = backgroundSprite[Random.Range(0, backgroundSprite.Length)];
                sr.sortingLayerName = sortingLayerName;
                
                Vector2 spriteSize = sr.sprite.bounds.size;
                Vector3 newScale = Vector3.one;
                newScale.x = _tileSize.x / spriteSize.x;
                newScale.y = _tileSize.y / spriteSize.y;
                tile.transform.localScale = newScale;

                // 타일 중앙 좌표 (청크 중심 기준)
                float tx = originX + (i + 0.5f) * _tileSize.x;
                float ty = originY - (j + 0.5f) * _tileSize.y;
                tile.transform.localPosition = new Vector3(tx, ty, 0);

                // 오브젝트 레이어 지정
                int layerIndex = LayerMask.NameToLayer(objectLayerName);
                if (layerIndex < 0)
                {
                    Debug.LogWarning($"{objectLayerName} 레이어를 찾을 수 없습니다.");
                    layerIndex = 0;
                }
                tile.layer = layerIndex;
            }
        }

        _chunkPool[key] = chunk;
        return chunk;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {

    }
#endif
}
