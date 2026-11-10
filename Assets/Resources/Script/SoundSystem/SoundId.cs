/// <summary>
/// 사운드 ID (docs/sound-design.md 6장, 5장 BGM)
/// - SoundEvent / SoundLibrary 에 int 로 직렬화되므로 값을 바꾸거나 재사용하지 말 것. 새 ID 는 빈 번호에 추가한다.
/// - 대역 : UI 100 / 레벨업·진행 200 / 플레이어·웨이브 300 / 무기 400 / 타격 500 / 몬스터 600 / BGM 900
/// </summary>
public enum SoundId
{
    None = 0,

    #region UI·메뉴 (100)
    UI_HOVER = 100,
    UI_CLICK = 101,
    UI_BACK = 102,
    UI_DENIED = 103,
    UI_SLIDER = 104,
    UI_START = 105,
    DIM_CURSOR = 110,
    DIM_CONFIRM_FOREST = 111,
    DIM_CONFIRM_FACTORY = 112,
    DIM_CONFIRM_STORM = 113,
    DIM_CANCEL = 114,
    DIM_READY = 115,
    #endregion

    #region 레벨업·진행 (200)
    LVL_UP = 200,
    LVL_SELECT = 201,
    LVL_WEAPON = 202,
    LVL_UPGRADE = 203,
    LVL_STAR = 204,
    LVL_BUFF = 205,
    LVL_DEBUFF = 206,
    PAUSE_OPEN = 210,
    PAUSE_CLOSE = 211,
    RESULT_WIN = 220,
    RESULT_LOSE = 221,
    #endregion

    #region 플레이어·웨이브 (300)
    PLR_HIT = 300,
    PLR_LOW_HP = 301,
    PLR_DEATH = 302,
    EXP_PICKUP = 303,
    WAVE_START = 310,
    WAVE_RUSH = 311,
    WAVE_FINAL = 312,
    WAVE_TICK = 313,
    #endregion

    #region 무기 (400)
    WPN_BOOMERANG = 400,
    WPN_POISON_THROW = 401,
    WPN_POISON_LAND = 402,
    WPN_SWEEP = 403,
    WPN_SPLIT_FIRE = 404,
    WPN_SPLIT_BURST = 405,
    WPN_VINE = 406,
    WPN_SPIRIT_SPAWN = 407,
    WPN_SPIRIT_HIT = 408,
    WPN_ROOT = 409,
    WPN_LASER = 410,
    WPN_DRONE = 411,
    WPN_MINE_SET = 412,
    WPN_MINE_BOOM = 413,
    WPN_SAW_LOOP = 414,
    WPN_FLAME_LOOP = 415,
    WPN_SPINBLADE_TELL = 416,
    WPN_SPINBLADE_LOOP = 417,
    WPN_CHAIN = 418,
    WPN_SHOCKWAVE = 419,
    WPN_EXPLOSIVE_FIRE = 420,
    WPN_EXPLOSIVE_BOOM = 421,
    WPN_TORNADO_LOOP = 422,
    WPN_LIGHTNING_MARK = 423,
    WPN_LIGHTNING_STRIKE = 424,
    WPN_STORMORB_LOOP = 425,
    #endregion

    #region 타격 (500)
    HIT_ORGANIC = 500,
    HIT_METAL = 501,
    HIT_SHOCK = 502,
    #endregion

    #region 몬스터 (600)
    MON_DIE_S = 600,
    MON_DIE_M = 601,
    MON_DIE_L = 602,
    MON_BOMBER_TELL = 603,
    MON_BOMBER_BOOM = 604,
    MON_TELEPORT = 605,
    MON_SUMMON = 606,
    MON_SPLIT = 607,
    MON_REVIVE = 608,
    MON_GOLEM_ENTER = 609,
    #endregion

    #region BGM (900)
    BGM_TITLE = 900,
    BGM_SELECT = 901,
    BGM_BASE_LOW = 902,
    BGM_BASE_MID = 903,
    BGM_BASE_HIGH = 904,
    BGM_LAYER_FOREST = 905,
    BGM_LAYER_FACTORY = 906,
    BGM_LAYER_STORM = 907,
    BGM_RUSH = 908,
    #endregion
}
