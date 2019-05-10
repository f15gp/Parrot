import socket
import json
import threading
from datetime import datetime
import time

class Connection:
    def __init__(self):
        """
        初期化

        :param なし

        :return なし
        """
        self.drone_ip = "192.168.42.1"
        self.is_connected = False
        self.udp_send_port = 44444
        self.udp_receive_port = 43210
        self.is_listening = True
        self.stream_port = 55004
        self.stream_control_port = 55005

    def _tcp_connection(self, num_retries):
        """
        ドローンにtcpで接続

        :param num_retries: 最大リトライ回数

        :return: True 接続 False 接続失敗
        """

        tcp_sock = socket.socket(family=socket.AF_INET, type=socket.SOCK_STREAM)
        print("connecting to ")
        tcp_sock.connect((self.drone_ip, 44444))

        # BEBOP 2に接続するためのjson要求
        # これしないとBEBOP2はうんともすんとも言わないｗ
        json_string = json.dumps({"d2c_port":self.udp_receive_port,
                                  "controller_type":"computer",
                                  "controller_name":"pyparrot",
                                  "arstream2_client_stream_port":self.stream_port,
                                  "arstream2_client_control_port":self.stream_control_port})

        json_obj = json.loads(json_string)
        print(json_string)
        tcp_sock.send(bytes(json_string, 'utf-8'))

        # Parrotからの応答を待つ
        finished = False
        num_try = 0
        while (not finished and num_try < num_retries):
            data = tcp_sock.recv(4096).decode('utf-8')
            if (len(data) > 0):
                my_data = data[0:-1]
                self.udp_data = json.loads(str(my_data))

                # Parrotから接続を拒否されたら、Falseをreturnする
                if (self.udp_data['status'] != 0):
                    return False

                print(self.udp_data)
                self.udp_send_port = self.udp_data['c2d_port']
                print("c2d_port is %d" % self.udp_send_port)
                finished = True
            else:
                num_try += 1

        # 後片付け
        tcp_sock.close()

        return finished

    def _udp_connection(self):
        """
        ドローンと接続

        :param なし

        :return: なし
        """
        print("udp_receive_port: ", int(self.udp_receive_port))

        self.udp_send_sock = socket.socket(family=socket.AF_INET, type=socket.SOCK_DGRAM)
        self.udp_receive_sock = socket.socket(family=socket.AF_INET, type=socket.SOCK_DGRAM)
        self.udp_receive_sock.settimeout(5.0)
        self.udp_receive_sock.bind(('0.0.0.0', int(self.udp_receive_port)))

    def _listen_socket(self):
        """
        ソケットからの受信を待ち合わせ

        :param なし

        :return: なし
        """
        print("starting listening at ")
        data = None

        while (self.is_listening):
            try:
                (data, address) = self.udp_receive_sock.recvfrom(66000)

            except socket.timeout:
                print("timeout - trying again")

            except:
                pass

            # 本当はここでデータに対しての処理が行われるが、とりあえず表示させる
            if (data):
                print(data)

    def disconnect(self):
        """
        ソケット切断
        """
        self.is_listening = False

        # 待ちあわせ
        self.smart_sleep(0.5)

        # クローズ
        try:
            self.udp_receive_sock.close()
            self.udp_send_sock.close()
        except:
            pass

    def smart_sleep(self, timeout):
        """
        要求された秒数眠る。が、通知があると起きる

        :param timeout: 寝る秒数
        :return:
        """

        start_time = datetime.now()
        new_time = datetime.now()
        diff = (new_time - start_time).seconds + ((new_time - start_time).microseconds / 1000000.0)

        while (diff < timeout):
            time.sleep(0.1)
            new_time = datetime.now()
            diff = (new_time - start_time).seconds + ((new_time - start_time).microseconds / 1000000.0)

    def connect(self):
        """
        ドローンと接続

        :param なし

        :return: True 接続 False 接続失敗
        """
        handshake = self._tcp_connection(5)
        if (handshake):
            self._udp_connection()
            self.listener_thread = threading.Thread(target=self._listen_socket)
            self.listener_thread.start()

            print("Success in setting up the wifi network to the drone!", "SUCCESS")
            return True
        else:
            print("Error: TCP handshake failed.", "ERROR")
            return False

def main():
    wifi = Connection()
    wifi.connect()

    # 3秒待つ
    wifi.smart_sleep(3.0)

    # 切断
    print("disconnecting...")
    wifi.disconnect()

    print("Bye!")

if __name__ == "__main__":
    main()
