﻿using System;
using System.Collections.Generic;
using Jypeli;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Graphics;
using XnaSoundEffect = Microsoft.Xna.Framework.Audio.SoundEffect;
using AudioChannels = Microsoft.Xna.Framework.Audio.AudioChannels;
using Jypeli.Widgets;
using System.Reflection;

static class Apuri
{
    public static PajaPeli Peli = null;

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

    public static void LataaKentatKansiosta(string kansio, int maksimiLeveys, int maksimiKorkeus,
        ref Dictionary<Image, string> nimet, out List<Image> kenttaKokoelma)
    {
        kenttaKokoelma = new List<Image>();

        string[] kuvatiedostonPolut = Directory.GetFiles(kansio, "*.png", SearchOption.TopDirectoryOnly);
        foreach (var kuvaPolku in kuvatiedostonPolut)
        {
            Image ladattuKuva = Image.FromFile(kuvaPolku);

            if (ladattuKuva.Width > maksimiLeveys || ladattuKuva.Height > maksimiKorkeus)
            {
                Peli.MessageDisplay.Add("Kenttäkuva " + Path.GetFileName(kuvaPolku) + " on liian suuri.");
                continue; // Hyppää seuraavaan tiedostoon (for silmukassa)
            }

            kenttaKokoelma.Add(ladattuKuva);
            nimet.Add(ladattuKuva, Path.GetFileNameWithoutExtension(kuvaPolku));
        }
    }


    public static void LataaKuvatKansiosta(string kansio, int vaadittuLeveys, int vaadittuKorkeus,
        ref Dictionary<Image, string> nimet, out Dictionary<Color, List<Image>> kuvaKokoelma)
    {
        kuvaKokoelma = new Dictionary<Color, List<Image>>();

        string[] kuvatiedostonPolut = Directory.GetFiles(kansio, "*.png", SearchOption.TopDirectoryOnly);
        foreach (var kuvaPolku in kuvatiedostonPolut)
        {
            Image ladattuKuva = Image.FromFile(kuvaPolku);

            if (ladattuKuva.Width != vaadittuLeveys || ladattuKuva.Height != vaadittuKorkeus)
            {
                Peli.MessageDisplay.Add("Kuva " + Path.GetFileName(kuvaPolku) + " on väärän kokoinen.");
                continue; // Hyppää seuraavaan tiedostoon (for silmukassa)
            }

            // Lue värikoodi
            Color varikoodi = ladattuKuva[0, 0];
            if (varikoodi == Color.White || varikoodi.AlphaComponent == 0)
            {
                Peli.MessageDisplay.Add("Kuvan " + Path.GetFileName(kuvaPolku) + " värikoodi on valkoinen tai läpinäkyvä, mitä ei sallita.");
                continue; // Hyppää seuraavaan tiedostoon (for silmukassa)
            }

            // Korvaa värikoodi naapuripikseleistä yleisimmällä.
            Color[] viereiset = new Color[] { ladattuKuva[1, 0], ladattuKuva[0, 1], ladattuKuva[1, 1] };
            ladattuKuva[0, 0] = Apuri.HaeYleisinVariKomponenteittain(viereiset);

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
     {"HYPPY", PajaPeli.Tapahtuma.Hyppaa },
     {"LIIKKUU", PajaPeli.Tapahtuma.Liikkuu },
     {"VOITTAA", PajaPeli.Tapahtuma.Voittaa },
     {"GAMEOVER", PajaPeli.Tapahtuma.PeliLoppuu }};

    

    public static void LataaAanetKansiosta(string kansio, out Dictionary<PajaPeli.Tapahtuma, List<SoundEffect>> tehosteKokoelma)
    {
        tehosteKokoelma = new Dictionary<PajaPeli.Tapahtuma, List<SoundEffect>>();

        string[] aaniTiedostoPolut = Directory.GetFiles(kansio, "*.wav", SearchOption.TopDirectoryOnly);
        foreach (var aaniPolku in aaniTiedostoPolut)
        {
            string tehosteenNimi = Path.GetFileNameWithoutExtension(aaniPolku);
            try
            {
                SoundEffect tehoste = LoadSoundEffectFromFile(aaniPolku);
                if (tehoste == null)
                    throw new NullReferenceException("Failed to load sound effect as loading routine returned null");

                // Tätä käyettiin resursseista lataamiseen ennen kuin LoadSoundEffectFromFile tehtiin
                //SoundEffect tehoste = Game.LoadSoundEffect(tehosteenNimi);

                // Lue avainsana
                PajaPeli.Tapahtuma tapahtuma = PajaPeli.Tapahtuma.Tuntematon;
                foreach (var osanimi in NimiTapahtumaksi.Keys)
                {
                    if (tehosteenNimi.ToUpper().Contains(osanimi))
                        tapahtuma = NimiTapahtumaksi[osanimi];
                }
                if (tapahtuma == PajaPeli.Tapahtuma.Tuntematon)
                {
                    Peli.MessageDisplay.Add("Tehostetta " + tehoste + ".wav ei osattu liittää mihinkään tapahtumaan. Tarkista tehosteen nimi.");
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
                Peli.MessageDisplay.Add("Äänitehoste " + tehosteenNimi + " on väärää tiedostomuotoa. Pyydä ohjaajalta apua.");
            }
        }
    }

    public static void LataaAanetKansiosta(string kansio, out Dictionary<string, SoundEffect> musiikkiKokoelma)
    {
        musiikkiKokoelma = new Dictionary<string, SoundEffect>();

        string[] aaniTiedostoPolut = Directory.GetFiles(kansio, "*.wav", SearchOption.TopDirectoryOnly);
        foreach (var aaniPolku in aaniTiedostoPolut)
        {
            string kappaleenNimi = Path.GetFileNameWithoutExtension(aaniPolku);
            try
            {
                SoundEffect kappale = LoadSoundEffectFromFile(aaniPolku);
                if (kappale == null)
                    throw new NullReferenceException("Failed to load sound effect as loading routine returned null");

                // Tätä käyettiin resursseista lataamiseen ennen kuin LoadSoundEffectFromFile tehtiin
                //SoundEffect kappale = Game.LoadSoundEffect(kappaleenNimi);
                musiikkiKokoelma[kappaleenNimi] = kappale;
            }
            catch (Exception)
            {
                Peli.MessageDisplay.Add("Musiikkikappaletta " + kappaleenNimi + " on väärää tiedostomuotoa. Pyydä ohjaajalta apua.");
            }
        }
    }

    // Valikko"hässäkkä
    public static void NaytaAlkuValikko()
    {
        MultiSelectWindow alkuValikko = new MultiSelectWindow("PajaPelin alkuvalikko",
                "Aloita satunnainen peli", "Valitse pelisi", "Lopeta");
        alkuValikko.AddItemHandler(0, Peli.SatunnainenPeliValittu);
        alkuValikko.AddItemHandler(1, ValitsePelaajaHahmo);
        alkuValikko.AddItemHandler(2, Peli.Exit);
        alkuValikko.DefaultCancel = 2;
        Peli.Add(alkuValikko);
    }

    public static void ValitsePelaajaHahmo()
    {
        List<string> hahmojenNimet = new List<string>();
        Dictionary<string, Image> nimistaKuvat = new Dictionary<string, Image>();
        foreach (var pelihahmo in Peli.HahmoKuvat[PajaPeli.PELAAJAN_ALOITUSPAIKAN_VARI])
        {
            hahmojenNimet.Add(Peli.Nimet[pelihahmo]);
            nimistaKuvat.Add(Peli.Nimet[pelihahmo], pelihahmo);
        }

        if (hahmojenNimet.Count > 0)
        {
            MultiSelectWindow pelaajaValikko = new MultiSelectWindow("Valitse pelaajahahmo", hahmojenNimet.ToArray());
            for (int i = 0; i < hahmojenNimet.Count; i++)
            {
                pelaajaValikko.AddItemHandler(i, PelihahmoValittu, i, hahmojenNimet);
            }
            Peli.Add(pelaajaValikko);
            Timer.SingleShot(0.1, () => NaytaNappienKuvat(pelaajaValikko, nimistaKuvat));
        }
        else
        {
            ValitseKartta();
        }
    }

    public static void NaytaNappienKuvat(MultiSelectWindow valikko, Dictionary<string, Image> nimistaKuvat)
    {
        foreach (PushButton valinta in valikko.Buttons)
        {
            valinta.TextColor = Color.Black;
            valinta.Image = nimistaKuvat[valinta.Text];
            valinta.ImageReleased = nimistaKuvat[valinta.Text];

            // Tee valinnasta vaaleampi
            Image valittuKuva = valinta.Image.Clone();
            valittuKuva.ApplyPixelOperation( c => Color.Lighter(c, 100) );
            valinta.ImageHover = valittuKuva;
        }
    }

    public static void PelihahmoValittu(int valinta, List<string> hahmojenNimet)
    {
        var res = Peli.Nimet
            .GroupBy(p => p.Value)
            .ToDictionary(g => g.Key, g => g.Select(pp => pp.Key).ToList());
        
        string valitunHahmonNimi = hahmojenNimet[valinta];
        Peli.ValittuPelaajaHahmo = res[valitunHahmonNimi].First();
        ValitseKartta();
    }

    public static void ValitseKartta()
    {
        List<string> karttojenNimet = new List<string>();
        Dictionary<string, Image> nimistaKuvat = new Dictionary<string, Image>();
        foreach (var kartta in Peli.Kartat)
        {
            karttojenNimet.Add(Peli.Nimet[kartta]);
            nimistaKuvat.Add(Peli.Nimet[kartta], kartta);
        }

        if (karttojenNimet.Count > 0)
        {
            MultiSelectWindow karttaValikko = new MultiSelectWindow("Valitse kartta", karttojenNimet.ToArray());
            for (int i = 0; i < karttojenNimet.Count; i++)
            {
                karttaValikko.AddItemHandler(i, KarttaValittu, i, karttojenNimet);
            }
            Peli.Add(karttaValikko);
            Timer.SingleShot(0.1, () => NaytaNappienKuvat(karttaValikko, nimistaKuvat));
        }
        else
        {
            ValitseTaustamusiikki();
        }
    }

    public static void KarttaValittu(int valinta, List<string> karttojenNimet)
    {
        var res = Peli.Nimet
           .GroupBy(p => p.Value)
           .ToDictionary(g => g.Key, g => g.Select(pp => pp.Key).ToList());

        string valitunKartanNimi = karttojenNimet[valinta];
        Peli.ValittuKartta = res[valitunKartanNimi].First();
        ValitseTaustakuva();
    }

    public static void ValitseTaustakuva()
    {
        List<string> taustojenNimet = new List<string>();
        Dictionary<string, Image> nimistaKuvat = new Dictionary<string, Image>();
        foreach (var tausta in Peli.Taustakuvat)
        {
            taustojenNimet.Add(Peli.Nimet[tausta]);
            nimistaKuvat.Add(Peli.Nimet[tausta], tausta);
        }

        if (taustojenNimet.Count > 0)
        {
            MultiSelectWindow taustaValikko = new MultiSelectWindow("Valitse taustakuva", taustojenNimet.ToArray());
            for (int i = 0; i < taustojenNimet.Count; i++)
            {
                taustaValikko.AddItemHandler(i, TaustaValittu, i, taustojenNimet);
            }
            Peli.Add(taustaValikko);
            //Timer.SingleShot(0.1, () => NaytaNappienKuvat(taustaValikko, nimistaKuvat));
        }
        else
        {
            ValitseTaustamusiikki();
        }
    }

    public static void TaustaValittu(int valinta, List<string> taustojenNimet)
    {
        var res = Peli.Nimet
           .GroupBy(p => p.Value)
           .ToDictionary(g => g.Key, g => g.Select(pp => pp.Key).ToList());

        string valitunTaustanNimi = taustojenNimet[valinta];
        Peli.ValittuTausta = res[valitunTaustanNimi].First();
        ValitseTaustamusiikki();
    }

    public static void ValitseTaustamusiikki()
    {
        List<string> musiikinNimet = new List<string>();
        foreach (string kappaleenNimi in Peli.Musiikki.Keys)
        {
            musiikinNimet.Add(kappaleenNimi);
        }
        musiikinNimet.Add("Ei musiikkia");

        if (musiikinNimet.Count > 0)
        {

            MultiSelectWindow musaValikko = new MultiSelectWindow("Valitse taustamusiikki", musiikinNimet.ToArray());
            for (int i = 0; i < musiikinNimet.Count; i++)
            {
                musaValikko.AddItemHandler(i, MusiikkiValittu, i, musiikinNimet);
            }
            Peli.Add(musaValikko);
        }
        else
        {
            Peli.TiettyPeliValittu();
        }
    }

    public static void MusiikkiValittu(int valinta, List<string> musiikinNimet)
    {
        if (valinta < musiikinNimet.Count-1)
        {
            string valitunKappaleenNimi = musiikinNimet[valinta];
            Peli.ValittuMusiikki = Peli.Musiikki[valitunKappaleenNimi];
        }
        Peli.TiettyPeliValittu();
    }


    public static void AsetaPeli(PhysicsGame peli)
    {
        Peli = peli as PajaPeli;
    }

    private static SoundEffect LoadSoundEffectFromFile(string wavPath)
    {
        SoundEffect jypeliSoundEffect = null;

        FileStream fs = new FileStream(wavPath, FileMode.Open, FileAccess.Read);
        BinaryReader reader = new BinaryReader(fs);

        //Read the wave file header from the buffer. (COPY/PASTE from MSDN)
        int chunkID = reader.ReadInt32();
        int fileSize = reader.ReadInt32();
        int riffType = reader.ReadInt32();
        int fmtID = reader.ReadInt32();
        int fmtSize = reader.ReadInt32();
        int fmtCode = reader.ReadInt16();
        int channels = reader.ReadInt16();
        int sampleRate = reader.ReadInt32();
        int fmtAvgBPS = reader.ReadInt32();
        int fmtBlockAlign = reader.ReadInt16();
        int bitDepth = reader.ReadInt16();

        if (fmtSize == 18)
        {
            // Read any extra values
            int fmtExtraSize = reader.ReadInt16();
            reader.ReadBytes(fmtExtraSize);
        }

        int dataID = reader.ReadInt32();
        int dataSize = reader.ReadInt32();

        // Read the data
        byte[] byteArray = reader.ReadBytes(dataSize);

        // Create the SoundEffect from the Stream
        XnaSoundEffect xnaSE = new XnaSoundEffect(byteArray, sampleRate, (AudioChannels)channels);

        // The needed constructor of Jypeli.SoundEffect is internal, so we cant simply do this:
        //SoundEffect sound = new SoundEffect( xnaSE );
        // instead we have to do this:
        jypeliSoundEffect =
            (SoundEffect)(Activator.CreateInstance(typeof(SoundEffect),
                BindingFlags.NonPublic | BindingFlags.Instance,
            null, new object[] { xnaSE }, null));

        return jypeliSoundEffect;
    }
}

public static class IDictionaryExtensions
{
    public static TKey FindKeyByValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TValue value)
    {
        if (dictionary == null)
            throw new ArgumentNullException("dictionary");

        foreach (KeyValuePair<TKey, TValue> pair in dictionary)
            if (value.Equals(pair.Value)) return pair.Key;

        throw new Exception("the value is not found in the dictionary");
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