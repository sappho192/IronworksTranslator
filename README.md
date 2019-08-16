# 아이언웍스 번역기 / Ironworks Translator  

파이널판타지 14 게임 내의 채팅을 한국어로 실시간 번역해줍니다.  
Translates chat log in FFXIV into Korean in realtime.  

![Honeycam 2019-08-17 00-19-58](https://user-images.githubusercontent.com/7788738/63179344-d8629200-c086-11e9-8ec9-d0325b919782.gif)

## 권장 실행환경  
Windows 10 64bit

## 요구 환경
.Net Framework 4.7.2 (다운로드: http://go.microsoft.com/fwlink/?linkid=863265 )

1. 파판14를 DirectX11 모드로 실행한다.  
2. IronworksTranslator.exe를 실행한다.  
3. "Attached ffxiv_dx11.exe" 메시지가 뜨는지 확인.  
4. 잠시 후 IronworksTranlator v0.0.1 이름을 가진 프로그램이 뜨면 적당한 곳에 두고 쓰면 됨.  
  
* 다운받고 첨으로 실행하면 준비작업 한다고 30초에서 1분? 정도 걸림.  
* 시스템 관련 메시지를 제외한 모든 채널의 메시지(파티, 외치기, 링쉘, 초보자 채널 등...)가 번역 대상임. 추후에 원하는 것만 할 수 있게 하겠음.  
* 강제종료했으면 작업관리자(Ctrl+Shift+Esc)가서 세부정보->PhantomJS.exe 까지 강제종료해주세여. 추후에 해결하겠음  

오류 제보, 기능 추가 문의는 https://github.com/sappho192/IronworksTranslator/issues 혹은 sappho192@지메일로 주세요.

## Dependencies (with NuGet) 
These dependency list is just for specification and will be already embedded.  
You don't have to install them to use this program.
 * Selenium, PhantomJS
 * Sharlayan

