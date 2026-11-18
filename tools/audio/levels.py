"""효과음 음량 표 (plinkfx 방식: 피크가 아니라 음량(LUFS)으로 맞춘다).
- 값은 '게임 안에서 들리는 음량' = 파일 음량 + SoundLibrary.groupGains(dB). 이벤트 volume 은 1 로 둔다.
- 측정: 최대 순간 음량 (ITU-R BS.1770 K-가중, 400 ms 창). 피크 상한 -1 dBTP.
  아주 짧은 소리(타격·클릭)는 피크 상한에 먼저 걸려 목표보다 작게 남을 수 있다 (plinkfx 와 같음).
- 원칙 (docs/sound-design.md 3장 4번): 자주 나는 소리일수록 작게. 루프는 계속 울리므로 원샷보다 작게.
  위험(Alert) > 진행·보상(드묾) > UI > 무기 > 몬스터 > 타격 > 경험치·루프
"""

# SoundLibrary.asset 의 groupGains 와 같아야 한다
GROUP_GAIN = {'Alert': -6, 'Reward': -10, 'UI': -12, 'Weapon': -14, 'Hit': -22, 'Monster': -20}

# ID → (그룹, 게임 안 목표 LUFS)
IN_GAME = {
    # 위험 (드묾, 가장 크게)
    'PLR_DEATH': ('Alert', -16), 'PLR_HIT': ('Alert', -21), 'MON_BOMBER_BOOM': ('Alert', -23),
    'MON_BOMBER_TELL': ('Alert', -25), 'PLR_LOW_HP': ('Alert', -29),
    # 진행·보상 (드묾)
    'LVL_UP': ('Reward', -21), 'RESULT_WIN': ('Reward', -22), 'RESULT_LOSE': ('Reward', -22),
    'LVL_STAR': ('Reward', -23), 'LVL_WEAPON': ('Reward', -24), 'LVL_UPGRADE': ('Reward', -24),
    'LVL_BUFF': ('Reward', -24), 'LVL_DEBUFF': ('Reward', -26),
    # 경험치: 초당 수십 번 나므로 가장 작게
    'EXP_PICKUP': ('Reward', -38),
    # UI
    'UI_START': ('UI', -26), 'DIM_READY': ('UI', -26), 'DIM_CONFIRM_FOREST': ('UI', -27),
    'DIM_CONFIRM_FACTORY': ('UI', -27), 'DIM_CONFIRM_STORM': ('UI', -27), 'UI_DENIED': ('UI', -30),
    'PAUSE_OPEN': ('UI', -31), 'PAUSE_CLOSE': ('UI', -31), 'DIM_CANCEL': ('UI', -32),
    'UI_CLICK': ('UI', -33), 'LVL_SELECT': ('UI', -33), 'UI_BACK': ('UI', -33),
    'UI_SLIDER': ('UI', -35), 'DIM_CURSOR': ('UI', -34),
    # 무기: 폭발류(드묾) > 발사(반복) > 루프(계속)
    'WPN_MINE_BOOM': ('Weapon', -29), 'WPN_EXPLOSIVE_BOOM': ('Weapon', -29),
    'WPN_LIGHTNING_STRIKE': ('Weapon', -29), 'WPN_SHOCKWAVE': ('Weapon', -30),
    'WPN_SPINBLADE_TELL': ('Weapon', -31), 'WPN_SPLIT_BURST': ('Weapon', -31),
    'WPN_BOOMERANG': ('Weapon', -32), 'WPN_LASER': ('Weapon', -32), 'WPN_CHAIN': ('Weapon', -33),
    'WPN_POISON_THROW': ('Weapon', -32), 'WPN_POISON_LAND': ('Weapon', -33), 'WPN_SWEEP': ('Weapon', -32),
    'WPN_SPLIT_FIRE': ('Weapon', -33), 'WPN_SPIRIT_SPAWN': ('Weapon', -32), 'WPN_ROOT': ('Weapon', -32),
    'WPN_DRONE': ('Weapon', -35), 'WPN_MINE_SET': ('Weapon', -34), 'WPN_EXPLOSIVE_FIRE': ('Weapon', -32),
    'WPN_LIGHTNING_MARK': ('Weapon', -32),
    'WPN_SAW_LOOP': ('Weapon', -37), 'WPN_FLAME_LOOP': ('Weapon', -36), 'WPN_SPINBLADE_LOOP': ('Weapon', -36),
    'WPN_TORNADO_LOOP': ('Weapon', -36), 'WPN_STORMORB_LOOP': ('Weapon', -37),
    # 몬스터
    'MON_DIE_L': ('Monster', -35), 'MON_DIE_M': ('Monster', -38), 'MON_DIE_S': ('Monster', -40),
    'MON_TELEPORT': ('Monster', -37), 'MON_SUMMON': ('Monster', -37), 'MON_REVIVE': ('Monster', -37),
    # 타격 (가장 잦음)
    'HIT_ORGANIC': ('Hit', -44), 'HIT_METAL': ('Hit', -45), 'HIT_SHOCK': ('Hit', -45),
}


def file_target(sound_id):
    """파일에 맞출 LUFS (게임 안 목표 - 그룹 음량). 표에 없으면 None (피크 정규화)"""
    if sound_id not in IN_GAME:
        return None
    group, lufs = IN_GAME[sound_id]
    return lufs - GROUP_GAIN[group]
