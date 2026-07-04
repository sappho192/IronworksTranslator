# PapagoTranslator 번역 결과 추출 로직 수정 구현 계획

## 1. 배경

기존 `PapagoTranslator`는 Papago 웹 페이지의 번역 결과가 `id="txtTarget"`인 HTML 요소 안에 존재한다는 전제에 의존하고 있었다.

```csharp
var pathElement = doc.GetElementbyId("txtTarget");
translated = pathElement.InnerText.Trim();
```

하지만 Papago 페이지 사양이 변경되면서 `txtTarget` 요소가 사라졌고, 현재 번역 결과는 Lexical editor 기반의 DOM 안에 렌더링된다.

현재 확인된 결과 영역의 주요 특징은 다음과 같다.

```html
<div
  data-testid="target-editor"
  role="textbox"
  contenteditable="false"
  aria-readonly="true"
  data-lexical-editor="true"
  lang="ja">
  <p dir="ltr">
    <span data-lexical-text="true">こんにちは！何が起きているのでしょうか？</span>
  </p>
</div>
```

따라서 앞으로는 `txtTarget` ID 기반 추출을 제거하고, 새 DOM 구조에 맞는 선택자와 렌더링 대기 로직을 도입한다.

---

## 2. 목표

### 2.1 필수 목표

- Papago의 변경된 DOM에서 번역 결과를 정상적으로 추출한다.
- CSS module class 이름에 의존하지 않는다.
- 추출 로직을 별도 함수로 분리하여 단위 테스트 가능하게 만든다.
- 번역 결과가 아직 렌더링되지 않은 경우를 고려해 재시도 또는 대기 로직을 둔다.
- 실패 시 기존 동작과 동일하게 원문을 반환하여 앱 전체 장애로 이어지지 않게 한다.

### 2.2 비목표

- Papago 내부 API를 직접 호출하는 방식으로 즉시 전환하지 않는다.
- 브라우저 자동화 프레임워크 전체를 교체하지 않는다.
- Papago 페이지 전체 구조를 완전히 모델링하지 않는다.

---

## 3. 문제 원인

현재 구현은 HTML을 한 번 받아온 뒤 `HtmlAgilityPack`으로 정적 파싱한다.

```csharp
var content = browser.Navigate(url);
var doc = new HtmlDocument();
doc.LoadHtml(content);
```

하지만 최근 Papago는 SPA/Next.js 형태로 동작하며, 번역 결과가 JavaScript 실행 후 동적으로 렌더링된다. 따라서 다음 두 문제가 동시에 발생할 수 있다.

1. `txtTarget` ID가 더 이상 존재하지 않는다.
2. `browser.Navigate(url)`이 렌더링 완료 전 HTML을 반환하면 새 선택자를 사용해도 결과를 찾지 못할 수 있다.

---

## 4. 구현 방침

### 4.1 선택자 우선순위

번역 결과 추출은 다음 순서로 시도한다.

1. `data-testid="target-editor"` 내부의 `data-lexical-text="true"` 텍스트
2. `data-testid="target-editor"` 요소 전체의 `InnerText`
3. `role="textbox"`, `aria-readonly="true"`, `contenteditable="false"`, `data-lexical-editor="true"` 조건을 만족하는 요소
4. 모든 방식이 실패하면 빈 문자열 반환

CSS class 이름은 다음과 같은 이유로 사용하지 않는다.

```html
class="text-editor-module-scss-module__gKzuvW__text-area ..."
```

이런 class 이름은 빌드 시 생성되는 CSS module 해시를 포함하므로, 배포 시점이나 빌드 변경에 따라 쉽게 바뀔 수 있다.

---

## 5. 단계별 구현 계획

## Phase 1: 추출 로직 분리

`RequestTranslate` 안에 직접 들어있던 DOM 추출 로직을 별도 순수 함수로 분리한다.

```csharp
private static string ExtractPapagoTargetText(string html)
{
    var doc = new HtmlDocument();
    doc.LoadHtml(html);

    var targetEditor =
        doc.DocumentNode.SelectSingleNode("//*[@data-testid='target-editor']")
        ?? doc.DocumentNode.SelectSingleNode(
            "//div[@role='textbox' and @aria-readonly='true' and @contenteditable='false' and @data-lexical-editor='true']"
        );

    if (targetEditor is null)
    {
        return string.Empty;
    }

    var lexicalTextNodes = targetEditor.SelectNodes(".//*[@data-lexical-text='true']");

    string rawText = lexicalTextNodes is { Count: > 0 }
        ? string.Concat(lexicalTextNodes.Select(node => node.InnerText))
        : targetEditor.InnerText;

    return System.Net.WebUtility.HtmlDecode(rawText).Trim();
}
```

### 기대 효과

- HTML fixture 기반 단위 테스트가 쉬워진다.
- Papago DOM이 다시 변경되어도 수정 범위가 작아진다.
- `RequestTranslate`는 브라우저 요청과 예외 처리만 담당하게 된다.

---

## Phase 2: 기존 `RequestTranslate`에 새 추출 로직 적용

우선 최소 변경으로 기존 흐름을 유지한다.

```csharp
private static async Task<string> RequestTranslate(string url)
{
    var browser = App.GetService<WebBrowser>();
    var content = browser.Navigate(url);

    return ExtractPapagoTargetText(content);
}
```

이 단계만으로도 `browser.Navigate(url)`이 렌더링 완료 후 HTML을 반환하는 구조라면 문제가 해결된다.

---

## Phase 3: 렌더링 대기 로직 도입

Phase 2 적용 후에도 번역 결과가 빈 문자열로 반환된다면, 원인은 DOM 구조가 아니라 렌더링 타이밍일 가능성이 높다.

이 경우 브라우저에서 JavaScript를 실행해 실제 DOM을 직접 읽어야 한다.

예시 JavaScript는 다음과 같다.

```javascript
(() => {
    const editor = document.querySelector('[data-testid="target-editor"]');
    if (!editor) return '';

    const lexicalNodes = editor.querySelectorAll('[data-lexical-text="true"]');
    if (lexicalNodes.length > 0) {
        return Array.from(lexicalNodes).map(x => x.textContent ?? '').join('').trim();
    }

    return (editor.innerText ?? '').trim();
})()
```

C# 쪽에서는 사용하는 `WebBrowser` 래퍼에 맞춰 다음과 같은 함수를 추가한다.

```csharp
private static async Task<string> WaitForRenderedTranslationAsync(
    WebBrowser browser,
    TimeSpan timeout,
    TimeSpan interval)
{
    var startedAt = DateTime.UtcNow;

    while (DateTime.UtcNow - startedAt < timeout)
    {
        string translated = await browser.ExecuteScriptAsync<string>("""
            (() => {
                const editor = document.querySelector('[data-testid="target-editor"]');
                if (!editor) return '';

                const lexicalNodes = editor.querySelectorAll('[data-lexical-text="true"]');
                if (lexicalNodes.length > 0) {
                    return Array.from(lexicalNodes).map(x => x.textContent ?? '').join('').trim();
                }

                return (editor.innerText ?? '').trim();
            })()
            """);

        if (!string.IsNullOrWhiteSpace(translated))
        {
            return translated.Trim();
        }

        await Task.Delay(interval);
    }

    return string.Empty;
}
```

> `ExecuteScriptAsync<string>`은 예시 이름이다. 현재 프로젝트의 `WebBrowser` 래퍼가 제공하는 실제 JavaScript 실행 API 이름에 맞춰 수정한다.

---

## Phase 4: `RequestTranslate` 최종 구조 정리

최종적으로는 정적 HTML 파싱과 렌더링 DOM 추출을 모두 지원하는 구조로 만든다.

```csharp
private static async Task<string> RequestTranslate(string url)
{
    var browser = App.GetService<WebBrowser>();
    var content = browser.Navigate(url);

    // 1차: Navigate 결과 HTML에서 추출
    var translated = ExtractPapagoTargetText(content);
    if (!string.IsNullOrWhiteSpace(translated))
    {
        return translated;
    }

    // 2차: 실제 렌더링된 DOM에서 추출
    translated = await WaitForRenderedTranslationAsync(
        browser,
        timeout: TimeSpan.FromSeconds(10),
        interval: TimeSpan.FromMilliseconds(300));

    return translated;
}
```

---

## 6. 예외 처리 정책

`TranslateAsync`의 기존 정책은 유지한다.

```csharp
try
{
    string translated = await RequestTranslate(url);
    return translated;
}
catch (Exception ex)
{
    Log.Error(ex, "Error translating with Papago");
    return sentence;
}
```

단, `RequestTranslate`가 빈 문자열을 반환하는 경우도 명시적으로 처리하는 것이 좋다.

```csharp
string translated = await RequestTranslate(url);

if (string.IsNullOrWhiteSpace(translated))
{
    Log.Warning("Papago returned empty translation. URL: {Url}", url);
    return sentence;
}

return translated;
```

---

## 7. 테스트 계획

## 7.1 단위 테스트

`ExtractPapagoTargetText`를 대상으로 HTML fixture 테스트를 작성한다.

### Case 1: 현재 Papago DOM

입력 HTML:

```html
<div data-testid="target-editor" data-lexical-editor="true" role="textbox" contenteditable="false">
  <p dir="ltr">
    <span data-lexical-text="true">こんにちは！何が起きているのでしょうか？</span>
  </p>
</div>
```

기대 결과:

```text
こんにちは！何が起きているのでしょうか？
```

### Case 2: 여러 span으로 나뉜 번역 결과

입력 HTML:

```html
<div data-testid="target-editor">
  <p>
    <span data-lexical-text="true">こんにちは！</span>
    <span data-lexical-text="true">何が起きているのでしょうか？</span>
  </p>
</div>
```

기대 결과:

```text
こんにちは！何が起きているのでしょうか？
```

### Case 3: `data-lexical-text`가 없는 경우

입력 HTML:

```html
<div data-testid="target-editor">
  こんにちは！何が起きているのでしょうか？
</div>
```

기대 결과:

```text
こんにちは！何が起きているのでしょうか？
```

### Case 4: 결과 요소가 없는 경우

입력 HTML:

```html
<html><body></body></html>
```

기대 결과:

```text

```

---

## 7.2 통합 테스트

다음 언어쌍에 대해 실제 번역 요청을 수행한다.

| Source | Target | Input | Expected |
|---|---|---|---|
| Korean | Japanese | 안녕하세요! 무슨 일이 일어나고 있나요? | 빈 문자열이 아닌 일본어 결과 |
| Japanese | Korean | こんにちは | 빈 문자열이 아닌 한국어 결과 |
| English | Korean | Hello | 빈 문자열이 아닌 한국어 결과 |

정확한 번역문은 Papago 모델 업데이트에 따라 바뀔 수 있으므로, 통합 테스트에서는 완전 일치보다 다음 조건을 우선한다.

- 결과가 빈 문자열이 아니다.
- 결과가 원문과 동일하지 않다.
- target language의 문자 특성이 대략적으로 포함된다.

---

## 8. 로깅 계획

장애 분석을 쉽게 하기 위해 다음 로그를 추가한다.

### 8.1 결과 요소를 찾지 못한 경우

```csharp
Log.Warning("Papago target editor element was not found.");
```

### 8.2 결과가 빈 문자열인 경우

```csharp
Log.Warning("Papago translation result was empty.");
```

### 8.3 예외 발생 시

기존 로그를 유지한다.

```csharp
Log.Error(ex, "Error translating with Papago");
```

단, URL 전체에는 원문 문장이 query string으로 들어가므로 개인정보나 민감 문장이 로그에 남지 않도록 주의한다.

권장 방식:

```csharp
Log.Error(ex, "Error translating with Papago. SourceLanguage: {SourceLanguage}, TargetLanguage: {TargetLanguage}", sourceLanguage, targetLanguage);
```

---

## 9. 리스크와 대응 방안

| 리스크 | 설명 | 대응 |
|---|---|---|
| DOM 재변경 | `data-testid="target-editor"`가 사라질 수 있음 | fallback selector 유지, 장애 로그 강화 |
| 렌더링 타이밍 문제 | 번역 결과가 아직 DOM에 반영되기 전에 HTML을 읽을 수 있음 | JavaScript 기반 wait/retry 도입 |
| CSS class 변경 | CSS module class는 빌드마다 바뀔 수 있음 | class selector 사용 금지 |
| Papago 접근 제한 | 자동화 접근이 차단되거나 동작이 불안정할 수 있음 | 공식 API 또는 다른 번역 provider fallback 검토 |
| 민감정보 로그 유출 | URL에 원문 문장이 포함될 수 있음 | URL 전체 로그 금지 |

---

## 10. 권장 최종 코드 구조

```csharp
public override async Task<string> TranslateAsync(
    string sentence,
    TranslationLanguageCode sourceLanguage,
    TranslationLanguageCode targetLanguage)
{
    if (!SupportedSourceLanguages.Contains(sourceLanguage))
    {
        Log.Error("Unsupported sourceLanguage");
        return sentence;
    }

    if (!SupportedTargetLanguages.Contains(targetLanguage))
    {
        Log.Error("Unsupported targetLanguage");
        return sentence;
    }

    string? sk = GetLanguageCode(sourceLanguage) ?? "ja";
    string? tk = GetLanguageCode(targetLanguage) ?? "en";

    string url = $"https://papago.naver.com/?sk={sk}&tk={tk}&st={Uri.EscapeDataString(sentence)}";

    try
    {
        string translated = await RequestTranslate(url);

        if (string.IsNullOrWhiteSpace(translated))
        {
            Log.Warning(
                "Papago returned empty translation. SourceLanguage: {SourceLanguage}, TargetLanguage: {TargetLanguage}",
                sourceLanguage,
                targetLanguage);

            return sentence;
        }

        return translated;
    }
    catch (Exception ex)
    {
        Log.Error(
            ex,
            "Error translating with Papago. SourceLanguage: {SourceLanguage}, TargetLanguage: {TargetLanguage}",
            sourceLanguage,
            targetLanguage);

        return sentence;
    }
}
```

```csharp
private static async Task<string> RequestTranslate(string url)
{
    var browser = App.GetService<WebBrowser>();
    var content = browser.Navigate(url);

    var translated = ExtractPapagoTargetText(content);
    if (!string.IsNullOrWhiteSpace(translated))
    {
        return translated;
    }

    // WebBrowser 래퍼가 JS 실행을 지원하는 경우에만 사용한다.
    // 지원하지 않는다면 WebBrowser 래퍼에 ExecuteScriptAsync 계열 API를 추가한다.
    translated = await WaitForRenderedTranslationAsync(
        browser,
        timeout: TimeSpan.FromSeconds(10),
        interval: TimeSpan.FromMilliseconds(300));

    return translated;
}
```

```csharp
private static string ExtractPapagoTargetText(string html)
{
    var doc = new HtmlDocument();
    doc.LoadHtml(html);

    var targetEditor =
        doc.DocumentNode.SelectSingleNode("//*[@data-testid='target-editor']")
        ?? doc.DocumentNode.SelectSingleNode(
            "//div[@role='textbox' and @aria-readonly='true' and @contenteditable='false' and @data-lexical-editor='true']"
        );

    if (targetEditor is null)
    {
        return string.Empty;
    }

    var lexicalTextNodes = targetEditor.SelectNodes(".//*[@data-lexical-text='true']");

    string rawText = lexicalTextNodes is { Count: > 0 }
        ? string.Concat(lexicalTextNodes.Select(node => node.InnerText))
        : targetEditor.InnerText;

    return System.Net.WebUtility.HtmlDecode(rawText).Trim();
}
```

---

## 11. 작업 순서

1. `ExtractPapagoTargetText` 함수 추가
2. 기존 `txtTarget` 기반 로직 제거
3. 첨부된 `Papago.html`을 fixture로 사용해 단위 테스트 추가
4. 최소 패치 버전으로 실제 번역 동작 확인
5. 빈 문자열 반환이 발생하면 렌더링 대기 로직 추가
6. `WebBrowser` 래퍼에 JavaScript 실행 API가 없다면 추가 구현
7. 로그에서 URL 전체가 남지 않도록 수정
8. 통합 테스트 수행
9. 릴리즈 후 Papago 장애 로그 모니터링

---

## 12. 완료 기준

- `txtTarget` 없이도 번역 결과를 추출할 수 있다.
- 첨부된 HTML fixture에서 `こんにちは！何が起きているのでしょうか？`를 반환한다.
- 결과가 렌더링 지연으로 비어 있는 경우 재시도할 수 있다.
- 실패 시 예외가 앱 밖으로 전파되지 않고 원문을 반환한다.
- 단위 테스트와 통합 테스트가 통과한다.
- URL 전체 또는 원문 문장이 로그에 직접 남지 않는다.
