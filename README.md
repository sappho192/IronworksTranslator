# 아이언웍스 번역기 / Ironworks Translator

파이널판타지 14 게임 내의 채팅을 한국어로 실시간 번역해줍니다.
Translates chat log in FFXIV into Korean in realtime.

## 동작 화면

### 1. 번역 과정

![Image01](images/demo01.gif)

### 2. NPC 대사 번역

![Image02](images/demo03.gif)

### 3. 외관 설정, 채널별 세부 설정

![Image03](images/demo02.gif)

### 4. 언어 설정

![Image03](images/languages.png)

## 권장 실행환경

Windows 10 64bit 이상

## 요구 환경

.NET 8 (다운로드: https://dotnet.microsoft.com/ko-kr/download/dotnet/thank-you/runtime-desktop-8.0.20-windows-x64-installer )

1. 파판14를 실행한다.
2. IronworksTranslator.exe를 실행한다.
3. (최초 실행 시) 이용 약관이 나오면 내용을 읽고 동의해주세요.
4. 번역 모델 준비 창이 없어질때까지 기다린다.
5. (최초 실행 시) 설정에서 모국어 설정과 채널별 언어 설정을 진행한다.
6. 취향에 따라 번역 모델을 선택한다.

오류 제보, 기능 추가 문의는 https://github.com/sappho192/IronworksTranslator/issues 혹은 [Discord](https://discord.gg/HJ8Y2sMjfu)로 문의바랍니다.

## 다운로드

최신 버전(1.0.0): [다운로드(풀버전)](https://github.com/sappho192/IronworksTranslator/releases/download/1.1.0/IronworksTranslator.1.1.1.zip)

[다운로드(경량버전)](https://github.com/sappho192/IronworksTranslator/releases/download/1.1.0/IronworksTranslator.1.1.1.minimal.zip)

풀버전은 자체 번역모델 파일(약 1GB)이 이미 포함되어있는 버전이며, 경량 버전은 제외되어 있습니다.
경량버전을 받아도 최초 실행 시에 자체 번역모델 파일을 다운로드하는 과정을 거치게 되니 참고해주세요.

[변경 내역](https://github.com/sappho192/IronworksTranslator/releases/latest)

## 사용 방법

### 채널별 언어 설정

프로그램 왼쪽 상단의 메뉴 아이콘을 클릭한 후, 세번째 메뉴(말풍선 모양)을 클릭하면 채널별 언어 설정이 가능합니다.
아래 그림과 같이 시스템 그룹의 언어를 클라이언트의 언어로 설정해두면 기본적인 시스템 메시지들이 본인의 언어로 번역됩니다.

![image](images/chat-lang-group.png)

채널 별로 설정을 다르게 하고 싶으면 아래 그림과 같이 메뉴를 확장하여 언어를 변경할 수 있습니다.

![image](images/chat-lang-chan.png)

### NPC 대사 번역

기본적으로 실시간으로 NPC 대사를 번역하는 방법이 활성화됩니다.
다만 실시간 방식은 매 패치 (6.1, 6.2, 6.3 등...) 마다 막힐 수 있습니다. 그땐 제가 직접 업데이트를 공개하기 전까진 채팅 로그 방식만 이용하실 수 있으니 두 가지 방법 모두 기억해주세요.

### [실시간 메모리 검사 방식] NPC 대사가 번역되어 표시되도록 하는 방법!

1. 번역기 설정에서 아래 그림과 같이 NPC 대사 번역 방식을 "Memory Search"로 바꿔주세요.
   ![image](images/memory-settings-01.png)
2. 채널별 설정→시스템에 있는 NPC 대사 언어를 본인 클라이언트 언어에 맞게 바꿔주세요.
   ![image](images/memory-settings-02.png)

### [채팅 로그 방식] NPC 대사가 번역되어 표시되도록 하는 방법!

채팅 로그 방식은 실시간 번역이 아니며, 구조적 한계 때문에 다음 대사로 넘어가야 이전 대사의 내용이 번역됩니다.

1. 아래 그림처럼 NPC 대사 번역 방식을 `Chat Message`로, 대사 언어를 본인의 클라이언트 언어에 맞게 바꿔주세요.

![chatnpc-settings-01](images/chatnpc-settings-01.png)

2. 채팅 설정으로 갑니다.

![image](https://user-images.githubusercontent.com/7788738/144707292-614ae1a7-3981-4ce4-966a-deeea6690125.png)

3. 로그 필터 중 General (맨 위에 있는 것)으로 들어갑니다.

![image](https://user-images.githubusercontent.com/7788738/144707307-1b688c4e-76fd-48be-b12c-0b59f99f98cd.png)

4. Announcements 탭 (3번째 탭)에서 NPC Dialogue에 체크가 되어있도록 합니다.

![image](images/npc-settings-03.png)

만약 게임 내 채팅창에 NPC 대사가 보이는게 싫은 분들은 아래와 같이 설정해주시면 됩니다.

1. 로그 필터 중 General (맨 위에 있는 것)으로 들어갑니다.

![image](images/npc-settings-04.png)

2. Announcements 탭 (3번째 탭)에서 NPC Dialogue에 **체크를 해제**합니다.

![image](images/npc-settings-05.png)

3. 로그 필터 중 Battle (두 번째)로 들어갑니다.

![image](images/npc-settings-06.png)

4. Announcements 탭 (3번째 탭)에서 NPC Dialogue에 체크가 되어있도록 합니다.

![image](images/npc-settings-07.png)

중요한 점은 4가지 로그 필터 중에 단 한 곳에만 NPC Dialogue가 체크되어있도록 하는 것이니 참고해주세요.
