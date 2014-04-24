using System;
using System.Collections.Generic;
using Jypeli;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Graphics;

static class Apurit
{
    public static void VaihdaKokoruuduntilaan(IntPtr Hwnd, bool ylin)
    {
        // Peli on kokoruudun kokoinen ja aina päällimmäinen.
        int HEADER_HT = 25; int BORDER_WT = 3;
        int screenHt = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
        int screenWt = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
        if (ylin)
            User32.SetWindowPos((uint)Hwnd, User32.HWND_TOPMOST, -BORDER_WT, -HEADER_HT, screenWt + BORDER_WT * 2, screenHt + HEADER_HT + BORDER_WT, 0);
        else
            User32.SetWindowPos((uint)Hwnd, User32.HWND_TOP, 0, 0, screenWt, screenHt, 0);
    }
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
            if (varikoodi == Color.White || varikoodi.AlphaComponent == 0)
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

    static Dictionary<string, PajaPeli.Tapahtuma> NimiTapahtumaksi = new Dictionary<string,PajaPeli.Tapahtuma>(){
     {"ISKEE", PajaPeli.Tapahtuma.Iskee},
     {"SATTUU", PajaPeli.Tapahtuma.Sattuu },
     {"KUOLEE", PajaPeli.Tapahtuma.Kuolee },
     {"ESINE", PajaPeli.Tapahtuma.Noukkii },
     {"LIIKKUU", PajaPeli.Tapahtuma.Liikkuu },
     {"GAMEOVER", PajaPeli.Tapahtuma.PeliLoppuu }};

    public static void LataaAanetKansiosta(string kansio, Game peli, out Dictionary<PajaPeli.Tapahtuma, List<SoundEffect>> tehosteKokoelma)
    {
        tehosteKokoelma = new Dictionary<PajaPeli.Tapahtuma, List<SoundEffect>>();

        string[] aaniTiedostoPolut = Directory.GetFiles(kansio, "*.wav", SearchOption.TopDirectoryOnly);
        foreach (var aaniPolku in aaniTiedostoPolut)
        {
            // TODO: SOUND EFFECTS CANNOT BE LOADED FROM FILE ;(   (for now)
            string tehosteenNimi = Path.GetFileNameWithoutExtension(aaniPolku);
            try
            {
                SoundEffect tehoste = Game.LoadSoundEffect(tehosteenNimi);

                // Lue avainsana
                PajaPeli.Tapahtuma tapahtuma = PajaPeli.Tapahtuma.Tuntematon;
                foreach (var osanimi in NimiTapahtumaksi.Keys)
                {
                    if (tehosteenNimi.ToUpper().Contains(osanimi))
                        tapahtuma = NimiTapahtumaksi[osanimi];
                }
                if (tapahtuma == PajaPeli.Tapahtuma.Tuntematon)
                {
                    peli.MessageDisplay.Add("Tehostetta " + tehoste + ".wav ei osattu liittää mihinkään tapahtumaan. Tarkista tehosteen nimi.");
                }
                else
                {
                    // Pistä kuva, siihen liittyvä värikoodi ja kuvan nimi talteen
                    if (!tehosteKokoelma.ContainsKey(tapahtuma))
                        tehosteKokoelma.Add(tapahtuma, new List<SoundEffect>());
                    tehosteKokoelma[tapahtuma].Add(tehoste);
                }
            }
            catch (Exception)
            {
                peli.MessageDisplay.Add("Äänitehoste " + tehosteenNimi + " ei ole pelin resursseissa. Pyydä ohjaajalta apua.");
            }
        }
    }

    public static void LataaAanetKansiosta(string kansio, Game peli, out Dictionary<string, SoundEffect> musiikkiKokoelma)
    {
        musiikkiKokoelma = new Dictionary<string, SoundEffect>();

        string[] aaniTiedostoPolut = Directory.GetFiles(kansio, "*.wav", SearchOption.TopDirectoryOnly);
        foreach (var aaniPolku in aaniTiedostoPolut)
        {
            // TODO: SOUND EFFECTS CANNOT BE LOADED FROM FILE ;(   (for now)
            string kappaleenNimi = Path.GetFileNameWithoutExtension(aaniPolku);
            try
            {
                SoundEffect kappale = Game.LoadSoundEffect(kappaleenNimi);
                musiikkiKokoelma[kappaleenNimi] = kappale;
            }
            catch (Exception)
            {
                peli.MessageDisplay.Add("Musiikkikappaletta " + kappaleenNimi + " ei ole pelin resursseissa. Pyydä ohjaajalta apua.");
            }
        }
    }
}


class User32
{
    public const int SW_MAXIMIZE = 3;
    public const int SW_MINIMIZE = 6;

    public const int HWND_TOP = 0;
    public const int HWND_TOPMOST = -1;

    [DllImport("user32.dll")]
    public static extern void SetWindowPos(uint Hwnd, int Level, int X, int Y, int W, int H, uint Flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, uint nCmdShow);
}