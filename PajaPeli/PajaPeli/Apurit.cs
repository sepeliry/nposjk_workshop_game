using System;
using System.Collections.Generic;
using Jypeli;
using System.Linq;
using System.IO;

static class Apurit
{
    public static Color HaeYleisinVariKomponenteittain(Color[] listaVareja)
    {
        return new Color(
            // Tämä sotku hakee vain yleisimmän pikselin värin naapureista.
                listaVareja.GroupBy(item => item.RedComponent).OrderByDescending(g => g.Count()).Select(g => g.Key).First(),
                listaVareja.GroupBy(item => item.GreenComponent).OrderByDescending(g => g.Count()).Select(g => g.Key).First(),
                listaVareja.GroupBy(item => item.BlueComponent).OrderByDescending(g => g.Count()).Select(g => g.Key).First(),
                listaVareja.GroupBy(item => item.AlphaComponent).OrderByDescending(g => g.Count()).Select(g => g.Key).First());
    }

    public static void LataaKartatKansiosta(string kansio, int maksimiLeveys, int maksimiKorkeus, Game peli,
        ref Dictionary<Image, string> nimet, out List<Image> karttaKokoelma)
    {
        karttaKokoelma = new List<Image>();

        string[] kuvatiedostonPolut = Directory.GetFiles(kansio, "*.png", SearchOption.TopDirectoryOnly);
        foreach (var kuvaPolku in kuvatiedostonPolut)
        {
            Image ladattuKuva = Image.FromFile(kuvaPolku);

            if (ladattuKuva.Width > maksimiLeveys || ladattuKuva.Height > maksimiKorkeus)
            {
                peli.MessageDisplay.Add("Karttakuva " + Path.GetFileName(kuvaPolku) + " on liian suuri.");
                continue; // Hyppää seuraavaan tiedostoon (for silmukassa)
            }

            karttaKokoelma.Add(ladattuKuva);
            nimet.Add(ladattuKuva, Path.GetFileNameWithoutExtension(kuvaPolku));
        }
    }


    public static void LataaKuvatKansiosta(string kansio, int vaadittuLeveys, int vaadittuKorkeus, Game peli,
        ref Dictionary<Image, string> nimet, out Dictionary<Color, List<Image>> kuvaKokoelma)
    {
        kuvaKokoelma = new Dictionary<Color, List<Image>>();

        string[] kuvatiedostonPolut = Directory.GetFiles(kansio, "*.png", SearchOption.TopDirectoryOnly);
        foreach (var kuvaPolku in kuvatiedostonPolut)
        {
            Image ladattuKuva = Image.FromFile(kuvaPolku);

            if (ladattuKuva.Width != vaadittuLeveys || ladattuKuva.Height != vaadittuKorkeus)
            {
                peli.MessageDisplay.Add("Kuva " + Path.GetFileName(kuvaPolku) + " on väärän kokoinen.");
                continue; // Hyppää seuraavaan tiedostoon (for silmukassa)
            }

            // Lue värikoodi
            Color varikoodi = ladattuKuva[0, 0];
            if (varikoodi == Color.White || varikoodi.AlphaComponent == 255)
            {
                peli.MessageDisplay.Add("Kuvan " + Path.GetFileName(kuvaPolku) + " värikoodi on valkoinen tai läpinäkyvä, mitä ei sallita.");
                continue; // Hyppää seuraavaan tiedostoon (for silmukassa)
            }

            // Korvaa värikoodi naapuripikseleistä yleisimmällä.
            Color[] viereiset = new Color[] { ladattuKuva[1, 0], ladattuKuva[0, 1], ladattuKuva[1, 1] };
            ladattuKuva[0, 0] = Apurit.HaeYleisinVariKomponenteittain(viereiset);

            // Pistä kuva, siihen liittyvä värikoodi ja kuvan nimi talteen
            if (!kuvaKokoelma.ContainsKey( varikoodi ))
                kuvaKokoelma.Add(varikoodi, new List<Image>());
            kuvaKokoelma[varikoodi].Add(ladattuKuva);
            nimet.Add(ladattuKuva, Path.GetFileNameWithoutExtension(kuvaPolku));
        }
    }

    public static List<Color> AnnaKaikkiKuvanVarit(Image kuva)
    {
        return kuva.GetData().Cast<Color>().ToList();
    }

    public static void LisaaKasittelijatVareille(List<Color> varit, ColorTileMap karttaLataaja, AbstractTileMap<Color>.TileMethod<Color> kasittelija, ref HashSet<Color> varatutVarit)
    {
        foreach (Color vari in varit)
        {
            if (!varatutVarit.Contains(vari))
            {
                karttaLataaja.SetTileMethod(vari, kasittelija, vari);
                varatutVarit.Add(vari);
            }
        }
    }
}
