using System;
using UnityEngine;
using System.IO.Ports;
using System.Threading;

public class STMController : MonoBehaviour
{
    // nazwa portu z ktorym bedziemy sie komunikowali, nalezy ustawic po przypisaniu obiektu na scenie na np "COM5"
    public String nazwaPortu;
    // obiekt do czytania/pisania z portu
    private SerialPort stm;
    // watek odczytujacy dane z stm'a
    private Thread stmThread;
    // obiekt do synchronizacji dostepu do odchylen, nie mozna synchronizowac na samych odchyleniach
    // bo sa uzywane przypisania do obiektu i za kazdym razem jest inny, trzeba utworzyc jakis staly, niezmienny obiekt do synchronizacji
    private System.Object odchyleniaMut = new System.Object();
    // aktualnie odczytane odchylenia
    public Vector3 odchylenia = new Vector3();
    // prędkość przesuwania paletki przy maksymalnym przyspieszeniu (w jednostkach/s)
    // co wazne, obiekt na scenie jest obrocony w osi Y o 90stopni, a translacji dokonujemy
    // relatywnie do obiektu, dlatego predkosc nalezy ustalic w osi Y
    // z tej samej osi akcelerometru bedzie korzystalo
    // mozna by zmienic zeby translacja byla relatywnie do swiata, wtedy byloby po x'ach
    public Vector3 predkosci = new Vector3();

	// Use this for initialization
    void Start()
    {
		nazwaPortu = COM.com;
        // utworzenie obiektu do komunikacji na danym porcie
        stm = new SerialPort(nazwaPortu, 38400, Parity.None, 8, StopBits.One);
        // proba otwarcia portu
        try
        {
            stm.Open();
            if (!stm.IsOpen)
            {
                // jezeli nie udalo sie utworzyc portu to komponent jest likwidowany
                Debug.LogWarning("Nie udalo sie otworzyc polaczenia z portem: " + nazwaPortu);
                Destroy(this);
            }
            else
            {
                Debug.Log("Otwarto polaczenie z portem: " + nazwaPortu);
                // udalo sie utworzyc polaczenie z portem - startuje watek do czytania danych z portu
                stmThread = new Thread(stmOdczyt);
                stmThread.Start();
            }
        }
        catch (Exception e)
        {
            // jezeli nie udalo sie utworzyc portu to komponent jest likwidowany
            Debug.LogWarning("Nie udalo sie otworzyc polaczenia z portem: " + nazwaPortu);
            Destroy(this);
        }
	}

    // funkcja do czytania z urzadzenia
    // aktualizuje aktualne odchylenia
    private void stmOdczyt()
    {
        // taka tam "nieskonczona" petla :D
        while (true)
        {
            // dzialamy na lokalnej zmiennej, zeby nie synchronizowac sie na 2 obiektach jednoczesnie (co by zakleszczen przypadkiem nie spowodowac)
            Vector3 odchylenia_l = new Vector3();
            // synchronizacja, zeby wiecej niz jeden watek nie korzystal z urzadzenia (OnDestroy dziala na innym watku)
            lock (stm)
            {
                // przerwanie petli w przypadku kiedy polaczenie zostanie zamkniete (spowoduje ladne zakonczenie watku)
                if (!stm.IsOpen) break;
                // odczyatnie danych z akcelerometru (x,y,z)
                odchylenia_l.x = stm.ReadByte();
                odchylenia_l.y = stm.ReadByte();
                odchylenia_l.z = stm.ReadByte();
                // pobrane dane są w formie liczby bez znaku <0,255> trzeba przeskalować wartości >127 jako ujemne zeby byl zakres <-128,127>
                // w liczbie ze znakiem
                // 1 = 00000001
                // 127 = 01111111
                // -128 = 10000000
                // -1 = 11111111
                // w liczbie bez znaku
                // 255 = 11111111
                // czyli -1 = 255
                // dlatego majac 255 wystarczy odjac 256 i mamy -1
                // 128 - 256 = -128
                // 10000000 = 10000000 :)
                if (odchylenia_l.x > 127)
                {
                    odchylenia_l.x -= 256;
                }
                if (odchylenia_l.y > 127)
                {
                    odchylenia_l.y -= 256;
                }
                if (odchylenia_l.z > 127)
                {
                    odchylenia_l.z -= 256;
                }
            }
            // synchronizacja, zeby wiecej niz 1 watek nie mial dostepu do odchylen
            // wrzucenie lokalnie odczytanych danych do pola w klasie (ustawienie aktualnych odchylen)
            lock (odchyleniaMut)
            {
                odchylenia = odchylenia_l;
            }
        }
    }

	// Update is called once per frame
    // na podstawie ostatniego odczytu z akcelerometru dokonuje odpowiedniego przesuniecia biorac pod uwage ustalona predkosc maksymalna
	void Update ()
	{
        Vector3 odchylenia_l;
        // synchronizacja, zeby wiecej niz 1 watek nie mial dostepu do odchylen, przekopiowanie danych do lokalnej zmiennej
	    lock (odchyleniaMut)
	    {
            // btw. to tworzy kopie tylko dlatego, ze Vector3 jest struktura
            // gdyby Vector3 byl klasa to operowali bysmy na referencjach, wiec to nadal bylby ten sam obiekt, trzeba by bylo
            // recznie zrobic kopie :)
	        odchylenia_l = odchylenia;
	    }
        // przeliczamy proporcjami predkosc, zakladajac ze dla odchylenia 127 = predkosc maksymalna
        // dla -127 = -predkosc maksymalna
	    Vector3 przesuniecie = predkosci;
	    przesuniecie.Scale(odchylenia_l);
	    przesuniecie /= 127;
        // predkosc jest okreslona w jednostkach na sekunde, deltaTime okresla ile sekund (czesci sekundy xD) zajelo generowanie klatki
	    przesuniecie *= Time.deltaTime;
        // przesuniecie obiektu
        transform.Translate(przesuniecie);
	}

    // w momencie kiedy komponent jest niszczony trzeba zamknac polaczenie (strumien) z urządzeniem
    void OnDestroy()
    {
        // synchronizacja, żeby 2 watki nie korzystaly z urzadzenia w tym samym momencie
        lock (stm)
        {
            // sprawdzenie czy polaczenie jest otwarte
            if (stm.IsOpen)
            {
                // zamkniecie polaczenia
                stm.Close();
                Debug.Log("Zamkniecie polaczenia z portem: "+nazwaPortu);
            }
        }
    }
}
