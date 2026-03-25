using System.Collections.Generic;

namespace CreateAlias;

public static class FfxivIcons
{
    public static readonly Dictionary<string, string> Map = new()
    {
        // ===== ICONS IDENTIFICADOS =====
        { "relogio", "\uE031" },
        { "clock", "\uE031" },

        { "check", "\uE03C" },
        { "ok", "\uE03C" },

        { "warning", "\uE03E" },

        { "plus", "\uE03F" },
        { "cruz", "\uE04F" },

        { "circulo", "\uE04C" },
        { "quadrado", "\uE04D" },
        { "triangulo", "\uE04E" },

        // números estilo ícone
        { "1", "\uE061" },
        { "2", "\uE062" },
        { "3", "\uE063" },
        { "4", "\uE064" },
        { "5", "\uE065" },
        { "6", "\uE066" },
        { "7", "\uE067" },
        { "8", "\uE068" },
        { "9", "\uE069" },

        // letras estilizadas
        { "a", "\uE070" },
        { "b", "\uE071" },
        { "c", "\uE072" },
        { "d", "\uE073" },
        { "e", "\uE074" },
        { "f", "\uE075" },

        // ===== FALLBACK AUTOMÁTICO =====
        // (para TODOS os outros do site)
        { "e020", "\uE020" },
        { "e021", "\uE021" },
        { "e022", "\uE022" },
        { "e023", "\uE023" },
        { "e024", "\uE024" },
        { "e025", "\uE025" },
        { "e026", "\uE026" },
        { "e027", "\uE027" },
        { "e028", "\uE028" },
        { "e029", "\uE029" },

        { "e040", "\uE040" },
        { "e041", "\uE041" },
        { "e042", "\uE042" },
        { "e043", "\uE043" },
        { "e044", "\uE044" },

        { "e050", "\uE050" },
        { "e051", "\uE051" },
        { "e052", "\uE052" },
        { "e053", "\uE053" },
        { "e054", "\uE054" },

        { "e090", "\uE090" },
        { "e091", "\uE091" },
        { "e092", "\uE092" },
        { "e093", "\uE093" },

        { "e0a0", "\uE0A0" },
        { "e0b0", "\uE0B0" },
        { "e0c0", "\uE0C0" },
        { "e0d0", "\uE0D0" },
        { "e0e0", "\uE0E0" }
    };
}