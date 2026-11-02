# 타이틀 씬
- Assets/Resources/Script/UI/Title/TitleMenu.cs: startButton(Button) 클릭 -> SceneManager.LoadScene(gameSceneName="DimensionSelectScene", 차원 선택 씬). public OnClickStart(). 이후 흐름: dimension.md
- Assets/Editor/Builders/TitleSceneBuilder.cs: Builders.TitleSceneBuilder.Build() (메뉴 Tools/Builders/TitleScene). TitleScene.unity를 빈 씬에서 재생성: Main Camera, EventSystem(InputSystemUIInputModule), TitleCanvas[Background RawImage=Textuer/fbg.png 임시, Logo TMP "2CTRL", MenuButtons(VerticalLayoutGroup)>StartButton "게임 시작"] + TitleMenu.
- Build Settings: Title(0), DimensionSelectScene(1), GameScene(2). 캡처 .claude/tmp/titlescene.png.
