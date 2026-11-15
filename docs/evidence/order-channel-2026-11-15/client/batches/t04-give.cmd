# t04 - two non-hotbar stacks for Empty Pockets. The row matters: `give` places the item at an
# explicit grid slot, so a second stack in row 1 is refused while row 1 is occupied (row 0 is the
# hotbar, and a hotbar stack is not handed over).
give Coal 6 1
give Wood 12 2
inv
