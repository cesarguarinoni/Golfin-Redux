BASE = ("Take the FIRST image - a photograph of a golf ball resting on a putting green - and replace the ball "
"in it with the ball shown in the SECOND image. Everything about the FIRST image stays exactly as it is: same "
"background, same grass, same flagstick, same lighting and shadow, same camera angle, same ball position and "
"same ball size in frame. Only the ball's livery changes. Copy the SECOND image's exact colours, graphics and "
"lettering onto a real dimpled golf ball, wrapped naturally around the sphere with correct perspective and the "
"dimple texture showing through the paint. {LOOK} {WORD} Photoreal product render, smooth painted urethane golf-ball "
"surface, no fabric or felt texture. Do not add anything else to the scene - no tee, no club, no second ball, no "
"hand, no extra flags, no signs. No other text or watermark anywhere, and do NOT draw any real-world brand logo of "
"any kind - no Nike mark, no tick, no check mark, no manufacturer emblem.")

WORD_ONE = ("The lettering must be spelled exactly {W}, printed once, level and fully readable, sized to sit inside "
"the ball's front face so it is not cut off at the edge of the ball.")
WORD_NONE = "There is no lettering on this ball - only the graphic mark, sized to sit inside the ball's front face."

BALLS = {
 "CIRQ": ("The ball is bright royal blue. On its front face: the word CIRQ in bold white capitals with a thin pale-cyan "
          "outline, the smaller word GOLF in spaced light-grey capitals directly beneath it, and a small yellow spiral swirl mark above.",
          WORD_ONE.format(W='"CIRQ" over "GOLF"')),
 "KLYRO": ("The ball is deep navy blue. The word KLYRO in white capitals is printed four times around a ring on the front face, "
           "with a small white-and-light-blue triangle chevron mark at the centre of the ring.",
           "Each repeat must be spelled exactly KLYRO - K L Y R O - and all repeats must stay on the visible face of the ball."),
 "SORALIS": ("The ball is dark navy-teal blue. A lime-yellow band runs around its equator carrying the word SORALIS in white "
             "capitals; at the left end of the band sits a lime crescent eclipse mark with a small white circle.",
             WORD_ONE.format(W='SORALIS')),
 "ROYAL": ("The ball is bright orange. A black band edged with thin white lines runs around its equator, carrying the word "
           "ROYAL in bold orange capitals followed by SWING in bold white capitals.",
           WORD_ONE.format(W='"ROYAL SWING"')),
 "MIREO": ("The ball is black. Gold Greek-key meander bands run near its top and bottom. On the front face is a round gold "
           "medallion with a Greek-key border, carrying MireO in black italic script with a small black sparkle star above and below.",
           WORD_ONE.format(W='MireO - capital M, i, r, e, capital O')),
 "GF": ("The ball is black. Thin red-and-white double pinstripes run around it above and below the front face. On the front "
        "face: G&F in white serif capitals with an ampersand, and a small red comma-shaped teardrop mark beneath it.",
        WORD_ONE.format(W='G&F')),
 "FAIRLOFT": ("The ball is white. On its front face is a black lozenge-shaped badge carrying FAIRLOFT in white capitals with a "
              "tiny JAPAN beneath, and two thin black lines cross behind the badge in an X.",
              WORD_ONE.format(W='FAIRLOFT with a tiny JAPAN beneath it')),
 "FAIRWAY": ("The ball is black. A vertical white stripe with a red stripe inside it runs from the top of the ball to the bottom "
             "through the front face. Over the stripe sits a round badge with a blue outline and cream fill, carrying Fairway in "
             "red italic script over THREADS in small blue capitals, with three tiny stars.",
             WORD_ONE.format(W='"Fairway" over "THREADS"')),
 "GOLFINIX": ("The ball is off-white. On its front face is a black rectangular badge with GOLFIN in bold white capitals "
              "immediately followed by IX in bold orange capitals, as one word GOLFINIX.",
              WORD_ONE.format(W='GOLFINIX - the letters G O L F I N in white then I X in orange')),
 "PARPERFECT": ("The ball is white. Pink and navy vertical stripes wrap the left flank of the ball. On the right of the front "
                "face: PAR in large blue italic capitals over PERFECT in smaller blue capitals.",
                WORD_ONE.format(W='"PAR" over "PERFECT"')),
 "BIRDIEV1": ("The ball is off-white cream. On its front face a small red crossed-arrows emblem sits above the word BIRDIE in "
              "black serif capitals.",
              WORD_ONE.format(W='BIRDIE - B I R D I E')),
 "TIFTO": ("The ball is off-white. On its front face is a cyan concentric-rings ripple mark like a fingerprint, with the tiny "
           "word VOIGT94 in cyan beneath it.",
           WORD_ONE.format(W='VOIGT94, small')),
 "ACEATTIRE": ("The ball is red. A wide golden-yellow band edged with thin white lines runs around its equator. On the band, in "
               "red, is the ACE ATTIRE monogram: one large capital A on the left, with the letters TTIRE on the upper line to its "
               "right and the letters CE on the lower line to its right, so the two words ATTIRE and ACE share the big A.",
               "The monogram must read ATTIRE on the top line and ACE on the bottom line, sharing one large A - no other spelling - "
               "printed once, level, and sized to sit inside the ball's front face."),
 "FYLOESOFT": ("The ball is white. A wide magenta-pink band runs around its equator carrying the word FYLOE in white capitals "
               "repeated along the band, with a bright green stripe edged by thin white lines above and below the magenta band.",
               "Each repeat must be spelled exactly FYLOE - F Y L O E - level on the band, and the repeat centred on the front face must be fully readable."),
 "FYLOEAIM": ("The ball is white. On its front face is a red crosshair target mark - a red ring with four short ticks - with a small red letter F at its centre.",
              WORD_NONE),
 "CLOVERPRO": ("The ball is mint teal-green. A large white four-petal clover shape covers most of its front face, with a small "
               "teal disc carrying a tiny white swirl at the clover's centre.",
               WORD_NONE),
 "GOLFINMK2": ("The ball is white. On its front face is one large, bold, chunky letter G in a green gradient running from dark "
               "green to bright lime, thicker and heavier than the G in the first image.",
               WORD_NONE),
 "SHIMMERG": ("The whole ball surface is an iridescent oil-slick rainbow - swirling purple, blue, green, yellow and orange like "
              "petrol on water - with a plain white disc at the centre of the front face carrying a single grey letter G.",
              WORD_NONE),
}

def prompt(tok):
    look, word = BALLS[tok]
    return BASE.format(LOOK=look, WORD=word)

if __name__ == "__main__":
    import sys
    print(prompt(sys.argv[1]))
