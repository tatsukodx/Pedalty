// ===== ピン定義 =====
#define RIGHT 3  // 右ボタン（D3）
#define LEFT 4   // 左ボタン（D4）
#define MAGNET 2 // マグネットセンサ（D2）・割り込み使用

// ===== マグネットセンサ用 =====
volatile bool magnetTriggered = false;
volatile unsigned long lastTriggerTime = 0;
volatile unsigned long triggerInterval = 0;

unsigned long ledOffTime = 0;
bool stoppedSent = true;

void calcVelocity() {
  unsigned long now = millis();
  if (lastTriggerTime != 0 && now - lastTriggerTime < 50)
    return; // チャタリング対策

  // 初回検出時は1秒間隔として扱い、最初の1回から走り始めるようにする
  triggerInterval = lastTriggerTime == 0 ? 1000 : now - lastTriggerTime;
  lastTriggerTime = now;
  magnetTriggered = true;
}

// ===== セットアップ =====
void setup() {
  Serial.begin(115200);

  pinMode(RIGHT, INPUT);
  pinMode(LEFT, INPUT);
  pinMode(MAGNET, INPUT_PULLUP);
  pinMode(LED_BUILTIN, OUTPUT);

  attachInterrupt(digitalPinToInterrupt(MAGNET), calcVelocity, FALLING);
}

// ===== メインループ =====
void loop() {
  // --- ボタン送信 ---
  int r = digitalRead(RIGHT);
  int l = digitalRead(LEFT);
  Serial.print(r);
  Serial.print(",");
  Serial.println(l);

  // --- マグネット送信 ---
  // 割り込みで更新される値を安全にコピーする
  noInterrupts();
  bool triggered = magnetTriggered;
  unsigned long interval = triggerInterval;
  unsigned long lastTrigger = lastTriggerTime;
  magnetTriggered = false;
  interrupts();

  if (triggered) {
    Serial.print("MAGNET,");
    Serial.println(interval);
    stoppedSent = false;

    // 磁石を検出したことをArduino本体のLEDで確認できるようにする
    digitalWrite(LED_BUILTIN, HIGH);
    ledOffTime = millis() + 80;
  } else if (!stoppedSent && lastTrigger != 0 && millis() - lastTrigger > 2000) {
    Serial.println("MAGNET,0");
    stoppedSent = true;
  }

  if (ledOffTime != 0 && (long)(millis() - ledOffTime) >= 0) {
    digitalWrite(LED_BUILTIN, LOW);
    ledOffTime = 0;
  }

  delay(50);
}
