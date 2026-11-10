using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 진행 이벤트 효과음 (SoundManager 가 같은 오브젝트에 붙인다)
/// - 웨이브 : 씬의 Spawner.OnWaveChanged. Spawner 가 진영마다 있으므로 같은 웨이브 번호는 한 번만 처리한다
///   0 번은 무음, SoundLibrary.RushWaveIndex 면 WAVE_RUSH, 그 Spawner 의 마지막 웨이브면 WAVE_FINAL, 그 외 WAVE_START (공유)
/// - 차원 선택 : DimensionSelectController 이벤트
///   OnCursorChanged → DIM_CURSOR(진영) / OnConfirmChanged(true) → 확정한 차원의 SoundLibrary.TryGetDimension(에셋 이름).confirm (진영)
///   OnConfirmChanged(false) → DIM_CANCEL(진영) / OnBothConfirmed → DIM_READY(공유) / OnConfirmDenied → UI_DENIED(진영)
/// - SoundManager 는 BeforeSceneLoad 에 생기므로 첫 씬의 sceneLoaded 도 받는다. 씬 오브젝트는 그때 Awake / OnEnable 이 끝나 있고 Start 전이다.
/// </summary>
[DisallowMultipleComponent]
public class FlowSoundBinder
    : MonoBehaviour
{
    private Spawner[] spawners = new Spawner[0];
    private DimensionSelectController dimensionSelect;

    // 이미 소리를 낸 웨이브 번호 (씬마다 리셋)
    private int lastWaveIndex = -1;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        BindScene();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnbindScene();
    }

    private void HandleSceneLoaded(Scene _scene, LoadSceneMode _mode)
    {
        BindScene();
    }

    #region Bind

    private void BindScene()
    {
        UnbindScene();
        lastWaveIndex = -1;

        spawners = FindObjectsByType<Spawner>(FindObjectsInactive.Include);
        foreach (Spawner s in spawners)
        {
            s.OnWaveChanged -= HandleWaveChanged;
            s.OnWaveChanged += HandleWaveChanged;
        }

        dimensionSelect = FindAnyObjectByType<DimensionSelectController>(FindObjectsInactive.Include);
        if (null != dimensionSelect)
        {
            dimensionSelect.OnCursorChanged += HandleCursorChanged;
            dimensionSelect.OnConfirmChanged += HandleConfirmChanged;
            dimensionSelect.OnBothConfirmed += HandleBothConfirmed;
            dimensionSelect.OnConfirmDenied += HandleConfirmDenied;
        }
    }

    private void UnbindScene()
    {
        // 파괴된 오브젝트라도 C# 델리게이트 해제는 안전하다
        foreach (Spawner s in spawners)
        {
            if (null != (object)s)
            {
                s.OnWaveChanged -= HandleWaveChanged;
            }
        }
        spawners = new Spawner[0];

        if (null != (object)dimensionSelect)
        {
            dimensionSelect.OnCursorChanged -= HandleCursorChanged;
            dimensionSelect.OnConfirmChanged -= HandleConfirmChanged;
            dimensionSelect.OnBothConfirmed -= HandleBothConfirmed;
            dimensionSelect.OnConfirmDenied -= HandleConfirmDenied;
        }
        dimensionSelect = null;
    }

    #endregion

    #region Wave

    private void HandleWaveChanged(Spawner _spawner, int _index)
    {
        if (_index <= lastWaveIndex)
        {
            return;
        }
        lastWaveIndex = _index;

        SoundId id = GetWaveSound(_spawner, _index);
        if (SoundId.None != id)
        {
            SoundManager.Play(id);
        }
    }

    /// <summary>
    /// 웨이브 번호의 소리 (0 번 무음 → 러시 → 마지막 → 일반 순서로 판정)
    /// </summary>
    public static SoundId GetWaveSound(Spawner _spawner, int _index)
    {
        if (_index <= 0)
        {
            return SoundId.None;
        }

        SoundLibrary library = SoundManager.HasInstance ? SoundManager.Instance.Library : null;
        if (null != library && _index == library.RushWaveIndex)
        {
            return SoundId.WAVE_RUSH;
        }
        if (null != _spawner && _index == _spawner.WaveCount - 1)
        {
            return SoundId.WAVE_FINAL;
        }
        return SoundId.WAVE_START;
    }

    #endregion

    #region Dimension Select

    private void HandleCursorChanged(PlayerSide _side, int _index)
    {
        SoundManager.Play(SoundId.DIM_CURSOR, _side);
    }

    private void HandleConfirmChanged(PlayerSide _side, bool _confirmed)
    {
        if (false == _confirmed)
        {
            SoundManager.Play(SoundId.DIM_CANCEL, _side);
            return;
        }

        SoundLibrary library = SoundManager.HasInstance ? SoundManager.Instance.Library : null;
        DimensionData data = null != dimensionSelect ? dimensionSelect.GetCurrent(_side) : null;
        if (null == library || null == data)
        {
            return;
        }
        if (true == library.TryGetDimension(data.name, out DimensionSoundEntry entry))
        {
            SoundManager.Play(entry.confirm, _side);
        }
    }

    private void HandleBothConfirmed()
    {
        SoundManager.Play(SoundId.DIM_READY);
    }

    private void HandleConfirmDenied(PlayerSide _side)
    {
        SoundManager.Play(SoundId.UI_DENIED, _side);
    }

    #endregion
}
