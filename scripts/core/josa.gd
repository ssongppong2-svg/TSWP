class_name Josa
extends RefCounted
## 한국어 조사 붙이기: 이름 끝 글자 받침에 따라 이/가 · 을/를 · 은/는 · 과/와
##  · 영문·숫자로 끝나면 읽는 소리로 대충 판단 (P2 → "투" 받침 없음 → 가)

const DIGIT_HAS := {"0": true, "1": true, "2": false, "3": true, "4": false, "5": false, "6": true, "7": true, "8": true, "9": false}
const LATIN_HAS := "lmnr"           # 이 글자로 끝나면 받침 소리 (영어 이름: Paul, Tom, Ann…)


static func has_batchim(word: String) -> bool:
	var w := word.strip_edges()
	if w == "":
		return false
	var c := w.unicode_at(w.length() - 1)
	if c >= 0xAC00 and c <= 0xD7A3:
		return (c - 0xAC00) % 28 != 0
	var ch := w.substr(w.length() - 1).to_lower()
	if DIGIT_HAS.has(ch):
		return DIGIT_HAS[ch]
	return LATIN_HAS.contains(ch)


static func i_ga(word: String) -> String:
	return word + ("이" if has_batchim(word) else "가")


static func eul_reul(word: String) -> String:
	return word + ("을" if has_batchim(word) else "를")


static func eun_neun(word: String) -> String:
	return word + ("은" if has_batchim(word) else "는")


static func gwa_wa(word: String) -> String:
	return word + ("과" if has_batchim(word) else "와")
