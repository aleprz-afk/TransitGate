// Firmware del ESP32-CAM (AI-Thinker, OV3660). Solo transmite video (Arquitectura §3):
//   http://<ip>/          página con el video, para mirar el encuadre
//   http://<ip>/capture   un JPEG suelto
//   http://<ip>:81/stream MJPEG para el servicio de visión (C3)
// Basado en el ejemplo CameraWebServer de Espressif, sin la interfaz de ajustes.
#include <WiFi.h>
#include "esp_camera.h"
#include "esp_http_server.h"
#include "config.h"   // copia de config.example.h con la red; ignorado por git (RNF-25)

#define LIMITE "transitgate-cuadro"
static const char *TIPO_STREAM = "multipart/x-mixed-replace;boundary=" LIMITE;
static const char *SEPARADOR = "\r\n--" LIMITE "\r\n";
static const char *CABECERA = "Content-Type: image/jpeg\r\nContent-Length: %u\r\n\r\n";

static const char PAGINA[] = R"(<!doctype html><meta name=viewport content="width=device-width">
<title>TransitGate CAM</title><body style="margin:0;background:#111">
<img id=v style="width:100%;max-width:640px;display:block;margin:auto">
<script>v.src='http://'+location.hostname+':81/stream'</script>)";

static bool iniciarCamara() {
  camera_config_t c = {};
  c.pin_pwdn = 32;  c.pin_reset = -1; c.pin_xclk = 0;
  c.pin_sccb_sda = 26; c.pin_sccb_scl = 27;
  c.pin_d7 = 35; c.pin_d6 = 34; c.pin_d5 = 39; c.pin_d4 = 36;
  c.pin_d3 = 21; c.pin_d2 = 19; c.pin_d1 = 18; c.pin_d0 = 5;
  c.pin_vsync = 25; c.pin_href = 23; c.pin_pclk = 22;
  c.xclk_freq_hz = 20000000;
  c.ledc_timer = LEDC_TIMER_0; c.ledc_channel = LEDC_CHANNEL_0;
  c.pixel_format = PIXFORMAT_JPEG;
  c.frame_size = FRAMESIZE_VGA;      // C3: VGA 640x480, calidad 12
  c.jpeg_quality = 12;
  c.fb_count = 2;
  c.fb_location = CAMERA_FB_IN_PSRAM;
  c.grab_mode = CAMERA_GRAB_LATEST;  // la visión quiere el cuadro más reciente, no la cola

  esp_err_t err = esp_camera_init(&c);
  if (err != ESP_OK) {
    Serial.printf("Error al iniciar la camara: 0x%x\n", err);
    return false;
  }
  sensor_t *s = esp_camera_sensor_get();
  s->set_vflip(s, VOLTEAR_VERTICAL);
  s->set_hmirror(s, ESPEJO_HORIZONTAL);
  return true;
}

static void conectarWifi() {
  IPAddress ip, puerta, mascara;
  if (ip.fromString(IP_FIJA) && puerta.fromString(IP_PUERTA) && mascara.fromString(IP_MASCARA)) {
    WiFi.config(ip, puerta, mascara, puerta);
  }
  WiFi.mode(WIFI_STA);
  WiFi.setSleep(false);              // el ahorro de energía baja mucho los FPS
  WiFi.setAutoReconnect(true);
  WiFi.begin(WIFI_SSID, WIFI_CLAVE);
  Serial.printf("Conectando a %s", WIFI_SSID);
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print('.');
  }
  Serial.printf("\nWiFi conectado, IP %s, RSSI %d dBm\n",
                WiFi.localIP().toString().c_str(), WiFi.RSSI());
}

static esp_err_t pagina(httpd_req_t *req) {
  httpd_resp_set_type(req, "text/html");
  return httpd_resp_send(req, PAGINA, HTTPD_RESP_USE_STRLEN);
}

static esp_err_t captura(httpd_req_t *req) {
  camera_fb_t *fb = esp_camera_fb_get();
  if (!fb) {
    httpd_resp_send_500(req);
    return ESP_FAIL;
  }
  httpd_resp_set_type(req, "image/jpeg");
  httpd_resp_set_hdr(req, "Access-Control-Allow-Origin", "*");
  esp_err_t r = httpd_resp_send(req, (const char *)fb->buf, fb->len);
  esp_camera_fb_return(fb);
  return r;
}

static esp_err_t stream(httpd_req_t *req) {
  esp_err_t r = httpd_resp_set_type(req, TIPO_STREAM);
  httpd_resp_set_hdr(req, "Access-Control-Allow-Origin", "*");
  char cabecera[64];
  uint32_t cuadros = 0, desde = millis();
  Serial.println("Stream: cliente conectado");

  while (r == ESP_OK) {
    camera_fb_t *fb = esp_camera_fb_get();
    if (!fb) {
      r = ESP_FAIL;
      break;
    }
    size_t n = snprintf(cabecera, sizeof cabecera, CABECERA, fb->len);
    r = httpd_resp_send_chunk(req, SEPARADOR, strlen(SEPARADOR));
    if (r == ESP_OK) r = httpd_resp_send_chunk(req, cabecera, n);
    if (r == ESP_OK) r = httpd_resp_send_chunk(req, (const char *)fb->buf, fb->len);
    size_t bytes = fb->len;
    esp_camera_fb_return(fb);

    // FPS cada 5 s en la consola serie, para revisar RNF-37 (≥ 15 FPS)
    cuadros++;
    uint32_t ahora = millis();
    if (ahora - desde >= 5000) {
      Serial.printf("Stream: %.1f FPS, ultimo cuadro %u bytes, RSSI %d dBm\n",
                    cuadros * 1000.0f / (ahora - desde), bytes, WiFi.RSSI());
      cuadros = 0;
      desde = ahora;
    }
  }
  Serial.println("Stream: cliente desconectado");
  return r;
}

static void iniciarServidores() {
  httpd_config_t c = HTTPD_DEFAULT_CONFIG();
  httpd_handle_t web = nullptr, video = nullptr;

  httpd_uri_t rutaPagina = { .uri = "/", .method = HTTP_GET, .handler = pagina };
  httpd_uri_t rutaCaptura = { .uri = "/capture", .method = HTTP_GET, .handler = captura };
  if (httpd_start(&web, &c) == ESP_OK) {
    httpd_register_uri_handler(web, &rutaPagina);
    httpd_register_uri_handler(web, &rutaCaptura);
  }

  // El stream va en su propio servidor (puerto 81) para no bloquear la página ni /capture
  c.server_port = 81;
  c.ctrl_port += 1;
  httpd_uri_t rutaStream = { .uri = "/stream", .method = HTTP_GET, .handler = stream };
  if (httpd_start(&video, &c) == ESP_OK) {
    httpd_register_uri_handler(video, &rutaStream);
  }
}

void setup() {
  Serial.begin(115200);
  pinMode(4, OUTPUT);
  digitalWrite(4, LOW);   // flash apagado
  Serial.println();
  if (!iniciarCamara()) {
    delay(5000);
    ESP.restart();
  }
  conectarWifi();
  iniciarServidores();
  Serial.printf("Listo: http://%s/  stream en http://%s:81/stream\n",
                WiFi.localIP().toString().c_str(), WiFi.localIP().toString().c_str());
}

void loop() {
  delay(10000);
}
