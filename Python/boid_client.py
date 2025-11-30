import socket
import json
import time
import numpy as np
from boid import Boid

width = 30
height = 30
num_boids = 20
flock = [Boid(*np.random.rand(2)*30, width, height) for _ in range(num_boids)]

def get_positions():
    pos_list = []
    for i, boid in enumerate(flock):
        boid.apply_behaviour(flock)
        boid.update()
        pos = boid.edges()
        position = {
            "x": float(pos.x),
            "y": 0.0,
            "z": float(pos.y)
        }
        pos_list.append(position)
        if i == 0:
            print(f"Boid 0: x={position['x']:.2f}, y={position['y']:.2f}, z={position['z']:.2f}")
    return pos_list

host, port = "127.0.0.1", 1101

print(f"Buscando servidor Unity en {host}:{port}...")

while True:
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
            sock.connect((host, port))
            print("¡Conectado a Unity!")
            
            while True:
                positions = get_positions()
                data = {"items": positions}
                json_str = json.dumps(data)
                print(f"Sending: {json_str[:100]}...")
                message = json_str + "\n"
                sock.sendall(message.encode('utf-8'))
                time.sleep(0.05)
                
    except ConnectionRefusedError:
        print("Servidor Unity no encontrado. Reintentando en 2 segundos...")
        time.sleep(2)
    except Exception as e:
        print(f"Error: {e}. Reiniciando conexión...")
        time.sleep(1)