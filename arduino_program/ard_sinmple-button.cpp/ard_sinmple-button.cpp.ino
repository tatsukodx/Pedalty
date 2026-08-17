#define RIGHT 3  // 右ボタン
#define LEFT 4   // 左ボタン
#define MAGNET 2 // マグネットセンサ・割り込み使用

// 全ピン内蔵プルアップ（ピン↔GND接続）。離している=HIGH、押している=LOW
#define IS_PRESSED(v) ((v) == LOW)

#define BUTTON_DEBOUNCE_MS 30

bool rightState = false;
bool leftState = false;
bool rightRaw = false;
bool leftRaw = false;
unsigned long rightChangedAt = 0;
unsigned long leftChangedAt = 0;

// 値が BUTTON_DEBOUNCE_MS の間変化しなかったときだけ状態を確定させる
bool debounce(bool raw, bool &lastRaw, bool &state, unsigned long &changedAt)
{
  unsigned long now = millis();
  if (raw != lastRaw)
  {
    lastRaw = raw;
    changedAt = now;
  }
  else if (raw != state && now - changedAt >= BUTTON_DEBOUNCE_MS)
  {
    state = raw;
  }
  return state;
}

volatile bool magnetTriggered = false;
volatile unsigned long lastTriggerTime = 0;
volatile unsigned long triggerInterval = 0;

unsigned long ledOffTime = 0;
bool stoppedSent = true;

void calcVelocity()
{
  unsigned long now = millis();
  if (lastTriggerTime != 0 && now - lastTriggerTime < 50)
    return; // チャタリング対策

  // 初回検出時は1秒間隔として扱い、最初の1回から走り始めるようにする
  triggerInterval = lastTriggerTime == 0 ? 1000 : now - lastTriggerTime;
  lastTriggerTime = now;
  magnetTriggered = true;
}

void setup()
{
  Serial.begin(115200);

  pinMode(RIGHT, INPUT_PULLUP);
  pinMode(LEFT, INPUT_PULLUP);
  pinMode(MAGNET, INPUT_PULLUP);
  pinMode(LED_BUILTIN, OUTPUT);

  attachInterrupt(digitalPinToInterrupt(MAGNET), calcVelocity, FALLING);
}

void loop()
{
  // 押=1 / 離=0 で送信
  bool r = debounce(IS_PRESSED(digitalRead(RIGHT)), rightRaw, rightState, rightChangedAt);
  bool l = debounce(IS_PRESSED(digitalRead(LEFT)), leftRaw, leftState, leftChangedAt);
  Serial.print(r ? 1 : 0);
  Serial.print(",");
  Serial.println(l ? 1 : 0);

  // 割り込みで更新される値を安全にコピーする
  noInterrupts();
  bool triggered = magnetTriggered;
  unsigned long interval = triggerInterval;
  unsigned long lastTrigger = lastTriggerTime;
  magnetTriggered = false;
  interrupts();

  if (triggered)
  {
    Serial.print("MAGNET,");
    Serial.println(interval);
    stoppedSent = false;

    digitalWrite(LED_BUILTIN, HIGH);
    ledOffTime = millis() + 80;
  }
  else if (!stoppedSent && lastTrigger != 0 && millis() - lastTrigger > 2000)
  {
    Serial.println("MAGNET,0");
    stoppedSent = true;
  }

  if (ledOffTime != 0 && (long)(millis() - ledOffTime) >= 0)
  {
    digitalWrite(LED_BUILTIN, LOW);
    ledOffTime = 0;
  }

  delay(50);
}
