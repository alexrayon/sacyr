import json

def obtener_respuesta_clima():
    """Simula una respuesta JSON de una API de meteorología"""
    datos = {
        "status": "success",
        "data": {
            "estacion_id": "Sacyr-Madrid-Norte",
            "viento_kmh": 48.7,
            "direccion": "NW",
            "ultima_actualizacion": "2026-04-13T10:00:00Z"
        }
    }
    return json.dumps(datos)

if __name__ == "__main__":
    print(obtener_respuesta_clima())