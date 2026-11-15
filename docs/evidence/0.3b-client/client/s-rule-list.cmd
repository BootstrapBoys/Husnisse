# Every chest's rule as the server holds it (position-addressed index: x, then z).
#   0 panel chest        1 metals            2 copy A            3 wood
#   4 copy B             5 overflow          6 deposit           7 off-network dump
#   8 plain networked    9 outside claim
#
# NOT used by driver.sh — it runs one scenario only, because every new scenario wipes the previous
# scene. This is the headless equivalent of the client's own `containers` dump, for a manual check.
husnisse_rule list
zdo piece_chest_wood husnisse_rule 0
zdo piece_chest_wood husnisse_rule 1
zdo piece_chest_wood husnisse_rule 2
zdo piece_chest_wood husnisse_rule 3
zdo piece_chest_wood husnisse_rule 4
zdo piece_chest_wood husnisse_rule 5
zdo piece_chest_wood husnisse_rule 6
zdo piece_chest_wood husnisse_rule 7
zdo piece_chest_wood husnisse_rule 8
zdo piece_chest_wood husnisse_rule 9
