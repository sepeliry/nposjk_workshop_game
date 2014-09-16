using System;
using System.Collections.Generic;
using Jypeli;
using Jypeli.Assets;
using Jypeli.Controls;
using Jypeli.Effects;
using Jypeli.Widgets;
using System.IO;

/*
 * TODO / BUGIT
 * 
 * TODO: Kokeile toimiiko dynaaminen lataaminen
 *          - hahmoille,
 *          - esineille ja
 *          - maastolle.
 * TODO: Tee lataaja Apurit luokkaan mp3 musiikille.
 * TODO: Valikot,
 *          - alkuvalikko, josta valitaan "Satunnainen peli", "Valikoitu peli" (ks alla.), "Lopeta".
 *          - "Valikoitu peli"-valikko, joilla voi (alivalikoista) valita kentän, pelihahmon ja taustamusiikin.
 *             Tee myös pikanäppäimet, joilla pelin aikana voi vaihtaa näitä.
 */

public class PajaPeli : PhysicsGame
{
    public enum Tapahtuma
	{
        Iskee,
        Kuolee,
        Sattuu,
        Noukkii,
        Liikkuu,
        PeliLoppuu,
        Tuntematon
	}

    // Nämä ovat peliin liittyviä vakioita. 
    //  - näitä voi kokeilla muuttaa
    static int PELAAJAN_KAVELYNOPEUS = 1000;
    static double PELAAJAN_LIUKUMISVAKIO = 0.1;
    static int ESTE_TUMMA_VARI_RAJAARVO = 200;
    public static Color PELAAJAN_ALOITUSPAIKAN_VARI = Color.FromPaintDotNet(0, 14);
    //  - näitä taas ei kannata kokeilla muuttaa
    static int RUUDUN_KUVAN_LEVEYS = 32;
    static int RUUDUN_KUVAN_KORKEUS = 32;
    static int RUUDUN_LEVEYS = 64;
    static int RUUDUN_KORKEUS = 64;

    static int KARTAN_MAKSIMILEVEYS = 100;
    static int KARTAN_MAKSIMIKORKEUS = 100;

    // Nämä pitävät sisällään peliin tiedostoista ladattavaa sisältöä.
    //  Dictionary tarkoittaa hakemistoa, jossa kuhunkin arvoon (esim. väri Color) on 
    //  linkitetty esim. lista kuvia (Image).
    public Dictionary<Image, string> Nimet = new Dictionary<Image,string>();
    public Dictionary<Color, List<Image>> HahmoKuvat;
    public Dictionary<Color, List<Image>> MaastoKuvat;
    public Dictionary<Color, List<Image>> EsineKuvat;
    public List<Image> Kartat;
    public Dictionary<Tapahtuma, List<SoundEffect>> Tehosteet;
    public Dictionary<string, SoundEffect> Musiikki;

    // Valitut asiat
    public Image ValittuPelaajaHahmo = null;
    public Image ValittuKartta = null;
    public SoundEffect ValittuMusiikki = null; 

    // Pelin tilanne ja tilatietoa tallentavat muuttujat
    List<Vector> PelaajanAloitusPaikat = new List<Vector>();
    PhysicsObject Pelaaja;

    // Liikkumisesta kuuluva ääni ja laskuri sen hiljentämiseksi
    Sound liikkumisAani = null;
    int liikutusNappejaPainettuna = 0;

    List<PhysicsObject> Hahmot = new List<PhysicsObject>();
    List<GameObject> Esineet = new List<GameObject>();

    public override void Begin()
    {
        //SetWindowSize(1280, 720);
        Apuri.Peli = this;

        // Ladataan peliin lisätty sisältö (taikuutta tapahtuu Apurit-luokassa)
        Apuri.LataaKuvatKansiosta(@"DynamicContent\Hahmot", RUUDUN_KUVAN_LEVEYS, RUUDUN_KUVAN_KORKEUS, ref Nimet, out HahmoKuvat);
        Apuri.LataaKuvatKansiosta(@"DynamicContent\Maasto", RUUDUN_KUVAN_LEVEYS, RUUDUN_KUVAN_KORKEUS, ref Nimet, out MaastoKuvat);
        Apuri.LataaKuvatKansiosta(@"DynamicContent\Esineet", RUUDUN_KUVAN_LEVEYS, RUUDUN_KUVAN_KORKEUS, ref Nimet, out EsineKuvat);
        Apuri.LataaKartatKansiosta(@"DynamicContent\Kartat", KARTAN_MAKSIMILEVEYS, KARTAN_MAKSIMIKORKEUS, ref Nimet, out Kartat);        

        // TODO: Tee lataaja äänille ja lataa
        Apuri.LataaAanetKansiosta(@"DynamicContent\Tehosteet", out Tehosteet);   
        Apuri.LataaAanetKansiosta(@"DynamicContent\Musiikki", out Musiikki);   

        // TODO: Käynnistä taustamusiikki, kun se loppuu kutsu aliohjelmaa, joka käynnistää 
        // TODO: Näytä valikko, jolla voi valita pelaajahahmon.
        // TODO: Näytä valikko, jolla voi valita kentän.

        Mouse.IsCursorVisible = true;

        Apuri.NaytaAlkuValikko();
        //Apuri.VaihdaKokoruuduntilaan(this.Window.Handle, true);
    }

    void LisaaNappainKuuntelijat()
    {
        Keyboard.Listen(Key.Escape, ButtonState.Pressed, ConfirmExit, "Lopeta peli");
        Keyboard.Listen(Key.Left,   ButtonState.Down, LiikutaPelaajaa, "Liikuta pelaajaa nuolinäppäimillä", new Vector( -PELAAJAN_KAVELYNOPEUS, 0 ));
        Keyboard.Listen(Key.Right,  ButtonState.Down, LiikutaPelaajaa, null, new Vector( PELAAJAN_KAVELYNOPEUS, 0 ));
        Keyboard.Listen(Key.Up,     ButtonState.Down, LiikutaPelaajaa, null, new Vector( 0, PELAAJAN_KAVELYNOPEUS ));
        Keyboard.Listen(Key.Down,   ButtonState.Down, LiikutaPelaajaa, null, new Vector( 0, -PELAAJAN_KAVELYNOPEUS ));
        Keyboard.Listen(Key.R,      ButtonState.Released, AloitaAlusta, "Palaa alkuvalikkoon");

        Keyboard.Listen(Key.Left, ButtonState.Pressed, AloitaLiike, null);
        Keyboard.Listen(Key.Right, ButtonState.Pressed, AloitaLiike, null);
        Keyboard.Listen(Key.Up, ButtonState.Pressed, AloitaLiike, null);
        Keyboard.Listen(Key.Down, ButtonState.Pressed, AloitaLiike, null);
        Keyboard.Listen(Key.Left, ButtonState.Released, LopetaLiike, null);
        Keyboard.Listen(Key.Right, ButtonState.Released, LopetaLiike, null);
        Keyboard.Listen(Key.Up, ButtonState.Released, LopetaLiike, null);
        Keyboard.Listen(Key.Down, ButtonState.Released, LopetaLiike, null);

        ShowControlHelp();
        
        // TODO: Tee näppäinkuuntelijat, joilla voi valita kentän, pelaajahahmon, taustamusan
    }

    public void AloitaAlusta()
    {
        ClearAll();
        Mouse.IsCursorVisible = true;
        Timer.SingleShot(1.0, () => Apuri.NaytaAlkuValikko());
    }
    public void AloitaSatunnainenPeli()
    {
        // kuva,   
        ValittuPelaajaHahmo = null;
        if (HahmoKuvat.ContainsKey(PELAAJAN_ALOITUSPAIKAN_VARI))
        {
            ValittuPelaajaHahmo = RandomGen.SelectOne<Image>(HahmoKuvat[PELAAJAN_ALOITUSPAIKAN_VARI]);
        }
        LisaaPelaajaPeliin();

        ValittuKartta = RandomGen.SelectOne<Image>(Kartat);
        LataaKentta();

        // äänet, 
        SoitaSatunnainenBiisi();
     
        // käy!
        Timer.SingleShot(0.1, TeeLoppuSilausPelille);
    }

    public void AloitaTiettyPeli()
    {
        LisaaPelaajaPeliin();
        LataaKentta();
        if (ValittuMusiikki != null)
        {
            Sound biisi = ValittuMusiikki.CreateSound();
            biisi.IsLooped = true;
            biisi.Play();
        }
        Timer.SingleShot(0.1, TeeLoppuSilausPelille);
    }
    
    void TeeLoppuSilausPelille()
    {
        LisaaNappainKuuntelijat();

        // Siirrä pelaaja aloituspaikkaan. Ja tuo se näkyviin.
        if (PelaajanAloitusPaikat.Count == 0)
            PelaajanAloitusPaikat.Add(new Vector(0, 0));
        Vector paikka = RandomGen.SelectOne<Vector>(PelaajanAloitusPaikat);
        Pelaaja.Position = paikka;
        Pelaaja.IsVisible = true;
        Pelaaja.IgnoresCollisionResponse = false;

        // Lisää maastoa esineiden ja hahmojen alle
        foreach (GameObject esine in Esineet)
        {
            LisaaTaustaMaasto(esine);
        }
        foreach (GameObject hahmo in Hahmot)
        {
            LisaaTaustaMaasto(hahmo);
            if (hahmo.Brain != null)
            {
                hahmo.Brain.Active = true;
            }
        }
        Mouse.IsCursorVisible = false;
    }
    
    void LataaKentta()
    {
        //1. Luetaan kuva uuteen ColorTileMappiin, kuvan nimen perässä ei .png-päätettä.
        ColorTileMap ruudut = new ColorTileMap( ValittuKartta );
  
        //2. Kerrotaan mitä aliohjelmaa kutsutaan, kun tietyn värinen pikseli tulee vastaan kuvatiedostossa.
        HashSet<Color> varatutVarit = new HashSet<Color>();
        Apuri.LisaaKasittelijatVareille(new List<Color>(MaastoKuvat.Keys), ruudut, LisaaMaastoaKartalle, ref varatutVarit);
        Apuri.LisaaKasittelijatVareille(new List<Color>(HahmoKuvat.Keys), ruudut, LisaaHahmoKartalle, ref varatutVarit);
        Apuri.LisaaKasittelijatVareille(new List<Color>(EsineKuvat.Keys), ruudut, LisaaEsineKartalle, ref varatutVarit);
        // Loput värit tulkitaan maastoksi
        Apuri.LisaaKasittelijatVareille(Apuri.AnnaKaikkiKuvanVarit(ValittuKartta), ruudut, LisaaMaastoaKartalle, ref varatutVarit);
        
        //3. Execute luo kentän
        //   Parametreina leveys ja korkeus
        ruudut.Execute(RUUDUN_LEVEYS+1, RUUDUN_KORKEUS+1);
    }

#region PeliOlioidenLisääminen
    void LisaaMaastoaKartalle(Vector paikka, double leveys, double korkeus, Color vari)
    {
        // Tumma väri, ei voi läpäistä.
        GameObject maastoOlio = null;
        if (vari.RedComponent+vari.BlueComponent+vari.GreenComponent < ESTE_TUMMA_VARI_RAJAARVO)
        {
            PhysicsObject este = PhysicsObject.CreateStaticObject(leveys, korkeus);
            maastoOlio = este;
            este.CollisionIgnoreGroup = 1; // Suorituskykyoptimointi
            Add(este, -1);

            // Kun pelaaja osuu hahmoon, kutsutaan PelaajaOsuuHahmoon aliohjelmaa
            AddCollisionHandler(Pelaaja, este, PelaajaOsuuEsteeseen);
        }
        // Vaalea väri, ihan vaan taustaa
        else
        {
            GameObject tausta = new GameObject(leveys, korkeus);
            maastoOlio = tausta;
            Add(tausta, -2);
        }

        maastoOlio.Color = vari;
        maastoOlio.Position = paikka;

        // Aseta kuva, jos sellainen on
        if (MaastoKuvat.ContainsKey(vari))
        {
            maastoOlio.Image = RandomGen.SelectOne<Image>(MaastoKuvat[vari]);
            maastoOlio.Tag = Nimet[maastoOlio.Image];
        }
    }
    void LisaaHahmoKartalle(Vector paikka, double leveys, double korkeus, Color vari)
    {
        // Magneta on pelaaja
        if (vari == PELAAJAN_ALOITUSPAIKAN_VARI)
        {
            PelaajanAloitusPaikat.Add(paikka);
        }
        else
        {
            PhysicsObject hahmo = new PhysicsObject(leveys, korkeus);
            hahmo.Position = paikka;
            hahmo.Image = RandomGen.SelectOne<Image>(HahmoKuvat[vari]);
            hahmo.Tag = Nimet[hahmo.Image];
            Add(hahmo, 2);
            Hahmot.Add(hahmo);

            // Lisää vihollisille aivot
            RandomMoverBrain aivot = new RandomMoverBrain(PELAAJAN_KAVELYNOPEUS / 10);
            aivot.TurnWhileMoving = true;
            aivot.Active = false; // pistetään päälle kun peli alkaa
            hahmo.Brain = aivot;

            // Kun pelaaja osuu hahmoon, kutsutaan PelaajaOsuuHahmoon aliohjelmaa
            AddCollisionHandler(Pelaaja, hahmo, PelaajaOsuuHahmoon);
        }
    }
    void LisaaEsineKartalle( Vector paikka, double leveys, double korkeus, Color vari)
    {
        PhysicsObject esine = new PhysicsObject(leveys-2, korkeus-2);
        esine.Image = RandomGen.SelectOne<Image>(EsineKuvat[vari]);
        esine.Tag = Nimet[esine.Image];
        esine.Position = paikka;
        Add(esine, 1);
        Esineet.Add(esine);

        // Kun pelaaja osuu esineeseen, kutsutaan PelaajaKeraaEsineen aliohjelmaa
        AddCollisionHandler(Pelaaja, esine, PelaajaKeraaEsineen);
    }
    void LisaaPelaajaPeliin()
    {
        Pelaaja = new PhysicsObject(RUUDUN_LEVEYS-2, RUUDUN_KORKEUS-2);
        Pelaaja.LinearDamping = 1-PELAAJAN_LIUKUMISVAKIO;
        Pelaaja.IsVisible = false;
        Pelaaja.IgnoresCollisionResponse = true;
        Add(Pelaaja, 2);
        Hahmot.Add(Pelaaja);

        if (ValittuPelaajaHahmo!=null)
        {
            Pelaaja.Image = ValittuPelaajaHahmo;
            Pelaaja.Tag = Nimet[ValittuPelaajaHahmo];
        }
        else
        {
            Pelaaja.Shape = Shape.Circle;
            Pelaaja.Color = PELAAJAN_ALOITUSPAIKAN_VARI;
        }
        Camera.Follow(Pelaaja);
    }
    void LisaaTaustaMaasto(GameObject esineTaiHahmo)
    {
        GameObject maasto = null;
        foreach (GameObject lahin in GetObjectsAt(esineTaiHahmo.Position, esineTaiHahmo.Width * 1.5))
        {
            // On esine itse tai este (tai hahmo)
            if (lahin == esineTaiHahmo || lahin is PhysicsObject)
                continue;
            maasto = lahin;
            break; // Lopettaa etsinnän
        }
        if (maasto != null)
        {
            GameObject uusiMaasto = new GameObject(maasto.Width, maasto.Height);
            uusiMaasto.Color = maasto.Color;
            uusiMaasto.Image = maasto.Image;
            uusiMaasto.Tag = maasto.Tag;
            uusiMaasto.Position = esineTaiHahmo.Position;
            Add(uusiMaasto);
        }
    }
#endregion

#region PeliTapahtumienKäsittely
    void PelaajaKeraaEsineen(PhysicsObject pelaaja, PhysicsObject esine)
    {
        ToistaTehoste(Tapahtuma.Noukkii);
        // TODO: Mitä sitten tapahtuu? Kirjoita koodia tähän...
    }
    void PelaajaOsuuHahmoon(PhysicsObject pelaaja, PhysicsObject hahmo)
    {
        ToistaTehoste(Tapahtuma.Kuolee);

        pelaaja.Destroy();

        Timer.SingleShot(0.5, PeliLoppuu);
        // TODO: Mitä sitten tapahtuu? Kirjoita koodia tähän...
    }
    void PelaajaOsuuEsteeseen(PhysicsObject pelaaja, PhysicsObject este)
    {
        ToistaTehoste(Tapahtuma.Sattuu);
        // TODO: Mitä sitten tapahtuu? Kirjoita koodia tähän...
    }
    void PeliLoppuu()
    {
        Label loppu = new Label("GAME OVER (paina ESC)");
        loppu.Font = Font.DefaultLargeBold;
        Add(loppu);
        ToistaTehoste(Tapahtuma.PeliLoppuu);
    }  
#endregion

#region ÄäntenSoitto
    void ToistaTehoste(Tapahtuma tapahtuma)
    {
        if (Tehosteet.ContainsKey(tapahtuma))
        {
            SoundEffect tehoste = RandomGen.SelectOne<SoundEffect>(Tehosteet[tapahtuma]);
            tehoste.Play();
        }
    }
    private void SoitaSatunnainenBiisi()
    {
        if (Musiikki.Count == 0)
            return;
        SoundEffect biisi = RandomGen.SelectOne<SoundEffect>(new List<SoundEffect>(Musiikki.Values));
        biisi.Play();

        // Kun biisi loppuu, aoita uusi.
        Timer.SingleShot(biisi.Duration.Seconds + 2.0, SoitaSatunnainenBiisi);
    }
#endregion

#region NapinPainallustenKäsittely
    void LiikutaPelaajaa(Vector vektori)
    {
        Pelaaja.Push(vektori);
        Pelaaja.Angle = Pelaaja.Velocity.Angle; // Käännä pelaajaa
    }

    void AloitaLiike()
    {
        if (liikutusNappejaPainettuna == 0 && Tehosteet.ContainsKey(Tapahtuma.Liikkuu))
        {
            liikkumisAani = RandomGen.SelectOne<SoundEffect>(Tehosteet[Tapahtuma.Liikkuu]).CreateSound();
            liikkumisAani.IsLooped = true;
            liikkumisAani.Volume = 0.05;
            liikkumisAani.Play();
        }
        liikutusNappejaPainettuna++;
    }
    void LopetaLiike()
    {
        liikutusNappejaPainettuna--;
        if (liikutusNappejaPainettuna == 0 && liikkumisAani!=null)
        {
            liikkumisAani.Stop();
        }
    }
    
#endregion
}
