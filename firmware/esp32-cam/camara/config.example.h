// Copia este archivo como config.h (ignorado por git) y llena los datos de la red.
#pragma once

#define WIFI_SSID  "nombre-de-la-red"
#define WIFI_CLAVE "clave-de-la-red"

// IP fija; en la red de la demo es 192.168.137.21 (Contratos de integración §10).
// Deja IP_FIJA vacía para usar DHCP.
#define IP_FIJA    ""
#define IP_PUERTA  ""
#define IP_MASCARA "255.255.255.0"

// Orientación de la imagen según cómo quede montada la cámara (0 o 1)
#define VOLTEAR_VERTICAL  0
#define ESPEJO_HORIZONTAL 0
