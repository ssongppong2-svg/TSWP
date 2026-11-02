class_name ChzzkChat
extends Node
## 치지직 채팅 읽기 (방송 연동 · 읽기 전용). 채널 ID 가 있으면 연결하고, 없으면 '가짜 시청자'가 대신 떠든다.
##  1) https://api.chzzk.naver.com/polling/v2/channels/{채널}/live-status → chatChannelId
##  2) https://comm-api.game.naver.com/nng_main/v1/chats/access-token?channelId=…&chatType=STREAMING → accessToken
##  3) wss://kr-ss{1~9}.chat.naver.com/chat 에 접속(cmd 100) → 채팅(cmd 93101) 받기 · 서버 핑(cmd 0)에 퐁(cmd 10000)
## ※ 비공식 경로라 치지직이 바꾸면 끊길 수 있다 — 그때는 자동으로 가짜 시청자로 넘어간다
##
## 신호 chat(닉네임, 글) — 투표(!1 !2 !3)·적 이름표에 쓴다

signal chat(nick: String, msg: String)
signal status_changed(text: String)

const UA := "Mozilla/5.0 (Windows NT 10.0; Win64; x64) TSWP"
const FAKE_NAMES := ["감자칩", "폭탄좋아", "고양이발", "새벽두시", "치킨무", "점프킹", "도토리묵", "버섯수프", "궁수지망생", "의사선생",
	"트롤장인", "왕관도둑", "바나나맛", "무지개떡", "구르기달인", "핑핑이", "숲속요정", "여왕님팬", "말랑콩떡", "탕후루"]

var connected := false
var status := "꺼짐"
var recent: Array = []              # 최근에 말한 닉네임 (적 이름표용)
var _ws: WebSocketPeer
var _cid := ""
var _token := ""
var _http: HTTPRequest
var _step := ""
var _ping_t := 0.0
var _fake_t := 0.0
var _vote_window := false           # 투표 중엔 가짜 시청자가 !1 !2 !3 을 친다
var vote_n := 3


func _ready() -> void:
	add_to_group("chat")
	process_mode = Node.PROCESS_MODE_ALWAYS
	_http = HTTPRequest.new()
	_http.timeout = 8.0
	add_child(_http)
	_http.request_completed.connect(_on_http)
	reconnect()


func reconnect() -> void:
	connected = false
	if _ws:
		_ws.close()
		_ws = null
	var ch := Prefs.chzzk_channel.strip_edges()
	if ch == "":
		_set_status("가짜 시청자 (채널 없음)")
		return
	_set_status("치지직 연결 중…")
	_step = "live"
	_http.cancel_request()
	var err := _http.request("https://api.chzzk.naver.com/polling/v2/channels/%s/live-status" % ch.uri_encode(), ["User-Agent: " + UA])
	if err != OK:
		_fail("요청 실패 (%d)" % err)


func _set_status(t: String) -> void:
	status = t
	status_changed.emit(t)


func _fail(why: String) -> void:
	connected = false
	_set_status("치지직 연결 안 됨: %s → 가짜 시청자" % why)
	get_tree().call_group("hud", "notify", "치지직 연결 안 됨 (%s)" % why, Color("#ff9a8a"))


func _on_http(result: int, code: int, _headers: PackedStringArray, body: PackedByteArray) -> void:
	if result != HTTPRequest.RESULT_SUCCESS or code != 200:
		_fail("응답 %d/%d" % [result, code])
		return
	var js: Variant = JSON.parse_string(body.get_string_from_utf8())
	if not (js is Dictionary) or not (js as Dictionary).has("content") or js["content"] == null:
		_fail("방송 정보 없음 (방송 중인지 확인)")
		return
	var content: Dictionary = js["content"]
	if _step == "live":
		_cid = String(content.get("chatChannelId", ""))
		if _cid == "" or _cid == "<null>":
			_fail("채팅 채널 없음 (방송을 켜야 한다)")
			return
		_step = "token"
		_http.request("https://comm-api.game.naver.com/nng_main/v1/chats/access-token?channelId=%s&chatType=STREAMING" % _cid, ["User-Agent: " + UA])
	elif _step == "token":
		_token = String(content.get("accessToken", ""))
		_open_socket()


func _open_socket() -> void:
	var n := 0
	for c in _cid:
		n += c.unicode_at(0)
	var server := (n % 9) + 1
	_ws = WebSocketPeer.new()
	var err := _ws.connect_to_url("wss://kr-ss%d.chat.naver.com/chat" % server)
	if err != OK:
		_fail("소켓 (%d)" % err)
		_ws = null
		return
	_step = "socket"


func _send(d: Dictionary) -> void:
	if _ws and _ws.get_ready_state() == WebSocketPeer.STATE_OPEN:
		_ws.send_text(JSON.stringify(d))


func _process(dt: float) -> void:
	if _ws:
		_ws.poll()
		var st := _ws.get_ready_state()
		if st == WebSocketPeer.STATE_OPEN and _step == "socket":
			_step = "open"
			_send({"ver": "2", "cmd": 100, "svcid": "game", "cid": _cid, "tid": 1,
				"bdy": {"uid": null, "devType": 2001, "accTkn": _token, "auth": "READ"}})
			connected = true
			_set_status("치지직 연결됨 — 채팅으로 투표 가능")
			get_tree().call_group("hud", "notify", "치지직 채팅 연결됨!", Color("#7affb0"))
		elif st == WebSocketPeer.STATE_CLOSED and _step in ["socket", "open"]:
			_step = ""
			_fail("소켓 닫힘")
			_ws = null
			return
		while _ws and _ws.get_available_packet_count() > 0:
			_on_packet(_ws.get_packet().get_string_from_utf8())
		_ping_t += dt
		if connected and _ping_t > 20.0:
			_ping_t = 0.0
			_send({"ver": "2", "cmd": 0})
	# 가짜 시청자: 연결이 없을 때만 (투표 중엔 자주)
	if not connected:
		_fake_t -= dt
		if _fake_t <= 0.0:
			_fake_t = randf_range(0.15, 0.5) if _vote_window else randf_range(3.0, 7.0)
			var nm: String = FAKE_NAMES[randi() % FAKE_NAMES.size()]
			var msg: String = ("!%d" % randi_range(1, vote_n)) if _vote_window else ["ㅋㅋㅋㅋ", "아 ㅋㅋ", "던져!", "폭탄 ㄱㄱ", "살려줘", "ㄷㄷ", "GG"][randi() % 7]
			_emit(nm, msg)


func _on_packet(txt: String) -> void:
	var js: Variant = JSON.parse_string(txt)
	if not (js is Dictionary):
		return
	var cmd := int(js.get("cmd", -1))
	if cmd == 0:
		_send({"ver": "2", "cmd": 10000})           # 서버 핑 → 퐁
		return
	if cmd != 93101 and cmd != 15101:
		return
	var bdy: Variant = js.get("bdy", [])
	if bdy is Dictionary:
		bdy = (bdy as Dictionary).get("messageList", [])
	if not (bdy is Array):
		return
	for m in bdy:
		if not (m is Dictionary):
			continue
		var msg := String(m.get("msg", m.get("content", "")))
		var nick := "시청자"
		var prof: Variant = m.get("profile", "")
		if prof is String and String(prof) != "":
			var pj: Variant = JSON.parse_string(String(prof))
			if pj is Dictionary:
				nick = String((pj as Dictionary).get("nickname", nick))
		_emit(nick, msg)


func _emit(nick: String, msg: String) -> void:
	if not recent.has(nick):
		recent.push_front(nick)
		if recent.size() > 30:
			recent.pop_back()
	chat.emit(nick, msg)


func set_vote(on: bool, n := 3) -> void:
	_vote_window = on
	_fake_t = 0.2
	vote_n = n


## 적 이름표용: 최근 채팅한 사람 (없으면 가짜 이름)
func random_viewer() -> String:
	if not recent.is_empty():
		return String(recent[randi() % recent.size()])
	return String(FAKE_NAMES[randi() % FAKE_NAMES.size()])
