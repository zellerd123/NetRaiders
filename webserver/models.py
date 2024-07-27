from pydantic import BaseModel
import time
import asyncio
import etcd3
import json
import logging
from typing import List
import math

# Defines how many hertz (ticks per second) that the network simulation will run at.
# Tick should always be a positive integer.
TICK_RATE = 20
SCALING_MULTIPLIER = ...

#### A pythonic representation of Unity3D's Vector2 Struct
class Vector2:
    def __init__(self, x : float, y : float):
        self.x = x
        self.y = y

    def __str__(self):
        return f"Vector2({self.x}, {self.y})"

    @staticmethod
    def move_towards(current, target, max_distance_delta):
        direction = Vector2(target.x - current.x, target.y - current.y)
        magnitude = math.sqrt(direction.x ** 2 + direction.y ** 2)
        if magnitude <= max_distance_delta or magnitude == 0:
            return target
        else:
            return Vector2(
                current.x + direction.x / magnitude * max_distance_delta,
                current.y + direction.y / magnitude * max_distance_delta
            )

    @staticmethod
    def distance(a, b):
        return math.sqrt((b.x - a.x) ** 2 + (b.y - a.y) ** 2)

class Circle:
    def __init__(self, position : Vector2, radius : float):
        self.position = position
        self.radius = radius
    
    @staticmethod
    def check_collision(ball1, ball2) -> bool:
        '''Takes two circle objects (player & byte) and determines if they are colliding with one another'''
        distance_squared = (ball1.position.x - ball2.position.x) ** 2 + (ball1.position.y - ball2.position.y) ** 2
        sum_of_radii_squared = (ball1.radius + ball2.radius) ** 2
        return distance_squared <= sum_of_radii_squared

    @staticmethod
    def check_full_overlap(ball1, ball2) -> bool:
        '''Checks if one circle is completely within another and if the smaller one's scale is <= 85% of the larger one's scale'''
        distance = math.sqrt((ball1.position.x - ball2.position.x) ** 2 + (ball1.position.y - ball2.position.y) ** 2)
        if ball1.radius > ball2.radius:
            larger = ball1
            smaller = ball2
        else:
            larger = ball2
            smaller = ball1
        if distance + smaller.radius <= larger.radius and smaller.radius <= 0.85 * larger.radius:
            return True
        return False


class BitPickup(BaseModel):
    id : int
    x : float
    y : float

class NetraiderPlayer(BaseModel):
    user_id : int
    username : str
    # the most recent authoritative tick that the player is on.
    tick : int = 0
    # what the players rtt is to the server, in terms of ticks instead of seconds
    tick_rtt : float = 0
    # x,y coordinates of player
    x : float = 0
    y : float = 0
    # how many bytes have been transmitted vs not transmitted.
    scale : float = 1
    untransmitted : int = 0
    transmitted : int = 0

class NetraiderSnapshot(BaseModel):
    local_player_id : int
    # what is server's most recent known tick (keeps client in sync)
    server_tick : int
    # what is the tick rate that the player should be using in their simulation?
    tick_rate : int = TICK_RATE
    # all of our players
    player_deltas : List[NetraiderPlayer] = []
    spawn_pickups : List[BitPickup] = []
    despawn_players : List[int] = []
    despawn_pickups : List[int] = []
    at_wap : bool = False
# Defines this inputs of the user. 
# This contains the tick at which they predict the server to currently be on, as well as the keys they pressed.
class NetraiderInput(BaseModel):
    expected_tick : float = 0
    x : float = 0
    y : float = 0