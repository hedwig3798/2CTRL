# 타이틀 씬
- Assets/Resources/Script/UI/Title/TitleMenu.cs: startButton(Button) 클릭 -> SceneManager.LoadScene(gameSceneName="DimensionSelectScene", 차원 선택 씬). public OnClickStart(). 이후 흐름: dimension.md
- Assets/Editor/Builders/TitleSceneBuilder.cs: Builders.TitleSceneBuilder.Build() (메뉴 Tools/Builders/TitleScene). TitleScene.unity를 빈 씬에서 재생성: Main Camera, EventSystem(InputSystemUIInputModule), TitleCanvas[Background RawImage=Textuer/fbg.png 임시, Logo TMP "2CTRL", MenuButtons(VerticalLayoutGroup)>StartButton "게임 시작"] + TitleMenu.
- Build Settings: Title(0), DimensionSelectScene(1), GameScene(2). 캡처 .claude/tmp/titlescene.png.
- 게임 종료: Script/UI/Title/QuitConfirmPanel.cs (자기 오브젝트 On/Off, Open/Close/IsOpen, confirmButton -> Quit(): 에디터는 isPlaying=false, 빌드는 Application.Quit, cancelButton -> Close(UI_BACK), event OnQuitRequested). TitleMenu: quitButton/quitPanel, IsAnyOpen 이면 다른 버튼 막음, ESC 는 종료 팝업부터 닫음.
- TitleSceneBuilder: MenuButtons 4개(Start/Upgrade/Settings/Quit, 높이 400), QuitCanvas(sortingOrder 60)>QuitConfirmPanel(Dim, 꺼짐)>Window>Message/Buttons(ConfirmButton, CancelButton). 문자열 키 ui.title.quit, ui.quit.message/confirm/cancel.
