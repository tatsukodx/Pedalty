#ifndef PEDALTY_CONFIG_H
#define PEDALTY_CONFIG_H

// ポテンショメータ（ハンドル角）
#define PIN_POT       A0
#define POT_PERIOD_MS 20 // 送信周期[ms] = 50Hz
#define POT_SAMPLES   8  // オーバーサンプリング回数（2の冪にすること）

// ボタン
#define PIN_BTN_RIGHT     3
#define PIN_BTN_LEFT      4
#define BTN_PERIOD_MS     50
#define BTN_DEBOUNCE_MS   30

// マグネットセンサ
#define PIN_MAGNET        2
#define MAGNET_STOP_MS    2000 // この時間パルスが無ければ MAGNET,0 を送る
#define MAGNET_CHATTER_MS 50

#endif
