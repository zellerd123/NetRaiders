from fastapi import (Request, Response, FastAPI, WebSocket)
from .proto_files import models_pb2
import base64
import logging
import json
import etcd3
import time
import random
from .netraidersimulation import *
import jwt
from typing import Dict

app = FastAPI()

@app.get("/whoami")
def whoami(request : Request):
    '''This endpoint will be in charge of issuing cookie to player for identifying them. Let them make persistent account, maintain leaderboard.'''
    return {"user": "BasicUser"}



'''Joins an active match'''
@app.get("/joinMatch/{username}")
def joinMatch(username : str, request : Request, response : Response):
    if len(username) < 3:
        return {'success': False, "message": "Username too short!"}
    # establish database connection
    database = etcd3.client(host="127.0.0.1", port=2379)
    # get all active matches
    current_matches = database.get_prefix('/activeMatches')
    # get all MatchIDs mapped to corresponding list of played connected to that match (player identified by username)
    matches_and_players : Dict[str, List[str]] = {}
    for match in current_matches:
        matches_and_players[(tuple[1].key.decode()).split("/")[-1]] = json.loads(tuple[0].decode())
    if len(matches_and_players) == 0:
        # no match! Make a new one.
        new_match_str = f"NR_{random.randint(0, 1000000000000)}"
        database.put(f'/activeMatches/{new_match_str}', value = [username])
        ...
    else:
        ...
    return {'success': True}

@app.get("/clearEtcd")
def clearEtcd():
    database = etcd3.client(host="127.0.0.1", port=2379)
    database.delete_prefix('/')
    return "Done"

@app.websocket("/netraiderConnect")
async def netraider(websocket : WebSocket):
    await websocket.accept()
    data = json.loads((await websocket.receive()).get("text", ""))
    username = data['username']
    user_id = random.randint(1, 100000)
    player = NetraiderPlayer(user_id = user_id, username = username)
    simulation = NetraidersSimulation()
    simulation.start_simulation(player)
    rtt_start = time.perf_counter()
    await websocket.send_text("ping")
    await websocket.receive()
    rtt_end = time.perf_counter()
    player.tick_rtt = (rtt_end - rtt_start) * simulation.tick_rate
    last_sent_tick = -1

    try:
        while True:
            rtt_start = time.perf_counter()
            # send players most recent state
            if last_sent_tick < player.tick:
                last_sent_tick = player.tick
                netraider_snapshot : NetraiderSnapshot = simulation.get_snapshot()
                await websocket.send_text(netraider_snapshot.json())
            # collect users inputs
            user_inputs = json.loads((await websocket.receive()).get("text", ""))       
            # takes client input and RTT and updates simulation
            simulation.handle_client_input(user_inputs)
            # rtt end.
            rtt_end = time.perf_counter()
            # set the RTT of the player and inform them of what it is.
            player.tick_rtt = (rtt_end - rtt_start) * simulation.tick_rate
    except Exception as e:
        try:
            logging.error(e)
            await websocket.close()
        except Exception as e:
            logging.error(e)
    finally:        
        simulation.end_simulation()
        logging.error(f'WebSocket Finally closed')