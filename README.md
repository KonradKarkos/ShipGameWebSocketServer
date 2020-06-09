# BattleShips game using really simple client with text commands

Server will keep game state even if both players disconnect. They can later join uisng game ID.
Communiaction is made through websockets using binary data.

Known issues (09.06.2020):
~~- Attacks on wrong boards.~~
~~- Miscalculating FatalHits.~~
~~- Allowing randomizing ships after connecting to already started game.~~

Usage:
 - Run server.
 - Run two clients, one of them has to create game. Second one has to join by using "Join game" button with game ID written in textarea. If there is no ID provided second client will join first available game.
 - Randomize ships till satisfied with given board.
 - Both players need to use "Ready" button to start the game.
 - Attack cooridantes by writing in textarea letter, then number and finally using button "Attack".
