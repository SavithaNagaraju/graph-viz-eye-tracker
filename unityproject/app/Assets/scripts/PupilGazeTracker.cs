﻿// Pupil Gaze Tracker service
// Written by MHD Yamen Saraiji
// https://github.com/mrayy

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;
using System;
using System.IO;
using MsgPack.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Pupil
{
	//Pupil data types based on Yuta Itoh sample hosted in https://github.com/pupil-labs/hmd-eyes
	[Serializable]
	public class ProjectedSphere
	{
		public double[] axes = new double[] { 0, 0 };
		public double angle;
		public double[] center = new double[] { 0, 0 };
	}

	[Serializable]
	public class Sphere
	{
		public double radius;
		public double[] center = new double[] { 0, 0, 0 };
	}

	[Serializable]
	public class Circle3d
	{
		public double radius;
		public double[] center = new double[] { 0, 0, 0 };
		public double[] normal = new double[] { 0, 0, 0 };
	}

	[Serializable]
	public class Ellipse
	{
		public double[] axes = new double[] { 0, 0 };
		public double angle;
		public double[] center = new double[] { 0, 0 };
	}

	[Serializable]
	public class PupilData3D
	{
		public double diameter;
		public double confidence;
		public ProjectedSphere projected_sphere = new ProjectedSphere ();
		public double theta;
		public int model_id;
		public double timestamp;
		public double model_confidence;
		public string method;
		public double phi;
		public Sphere sphere = new Sphere ();
		public double diameter_3d;
		public double[] norm_pos = new double[] { 0, 0, 0 };
		public int id;
		public double model_birth_timestamp;
		public Circle3d circle_3d = new Circle3d ();
		public Ellipse ellipese = new Ellipse ();
	}

	[Serializable]
	public class GazeNormals3d
	{
		public double[] __invalid_name__0;
		public double[] __invalid_name__1;
	}

	[Serializable]
	public class EyeCenters3d
	{
		public double[] __invalid_name__0;
		public double[] __invalid_name__1;
	}

	[Serializable]
	public class BaseData2
	{
		public int model_id;
		public Circle3d circle_3d = new Circle3d ();
		public string method;
		public double timestamp;
		public double[] norm_pos = new double[] { 0, 0, 0 };
		public double diameter_3d;
		public double confidence;
		public Sphere sphere = new Sphere ();
		public double phi;
		public double model_birth_timestamp;
		public string topic;
		public double diameter;
		public double model_confidence;
		public double theta;
		public ProjectedSphere projected_sphere = new ProjectedSphere ();
		public int id;
		public Ellipse ellipse = new Ellipse ();
	}

	[Serializable]
	public class BaseData
	{
		public GazeNormals3d gaze_normals_3d = new GazeNormals3d ();
		public string topic;
		public EyeCenters3d eye_centers_3d = new EyeCenters3d ();
		public double timestamp;
		public double[] norm_pos = new double[] { 0, 0, 0 };
		public double confidence;
		public double[] gaze_point_3d = new double[] { 0, 0, 0 };
		public BaseData2[] base_data;
	}

	[Serializable]
	public class GazeOnSrf
	{
		public float[] norm_pos = new float[] { 0, 0, 0 };
		public string topic;
		public BaseData base_data = new BaseData ();
		public bool on_srf;
		public double confidence;
	}

	[Serializable]
	public class PupilGazeOnSurface
	{
		public double[][] m_from_screen;
		public string name;
		public string uid;
		public double timestamp;
		public double[][] m_to_screen;
		public double[][] camera_pose_3d;
		public GazeOnSrf[] gaze_on_srf;
	}
}

public class PupilGazeTracker:MonoBehaviour
{
	static PupilGazeTracker _Instance;
	public GameObject camera;
	private udpsocket udpsocketScript;

	public static PupilGazeTracker Instance {
		get {
			if (_Instance == null) {
				_Instance = new GameObject ("PupilGazeTracker").AddComponent<PupilGazeTracker> ();
			}
			return _Instance;
		}
	}

	class MovingAverage
	{
		List<float> samples = new List<float> ();
		int length = 5;

		public MovingAverage (int len)
		{
			length = len;
		}

		public float AddSample (float v)
		{
			samples.Add (v);
			while (samples.Count > length) {
				samples.RemoveAt (0);
			}
			float s = 0;
			for (int i = 0; i < samples.Count; ++i)
				s += samples [i];

			return s / (float)samples.Count;

		}
	}

	class EyeData
	{
		MovingAverage xavg;
		MovingAverage yavg;

		public EyeData (int len)
		{
			xavg = new MovingAverage (len);
			yavg = new MovingAverage (len);
		}

		public Vector2 gaze = new Vector2 ();

		public Vector2 AddGaze (float x, float y)
		{
			gaze.x = xavg.AddSample (x);
			gaze.y = yavg.AddSample (y);
			return gaze;
		}
	}

	EyeData leftEye;
	EyeData rightEye;

	Vector2 _eyePos;
	float confidence;


	Thread _serviceThread;
	bool _isDone = false;
	Pupil.PupilData3D _pupilData;
	Pupil.PupilGazeOnSurface _pupilGazeOnSurface;

	public delegate void OnCalibrationStartedDeleg (PupilGazeTracker manager);

	public delegate void OnCalibrationDoneDeleg (PupilGazeTracker manager);

	public delegate void OnEyeGazeDeleg (PupilGazeTracker manager);

	public delegate void OnCalibDataDeleg (PupilGazeTracker manager, float x, float y);

	public event OnCalibrationStartedDeleg OnCalibrationStarted;
	public event OnCalibrationDoneDeleg OnCalibrationDone;
	public event OnEyeGazeDeleg OnEyeGaze;
	public event OnCalibDataDeleg OnCalibData;


	bool _isconnected = false;
	RequestSocket _requestSocket;

	List<Dictionary<string,object>> _calibrationData = new List<Dictionary<string,object>> ();

	[SerializeField]
	Dictionary<string,object>[] _CalibrationPoints {
		get{ return _calibrationData.ToArray (); }
	}


	Vector2[] _calibPoints;
	int _calibSamples;
	int _currCalibPoint = 0;
	int _currCalibSamples = 0;

	public string ServerIP = "";
	public int ServicePort = 50020;
	public int DefaultCalibrationCount = 120;
	public int SamplesCount = 4;
	public float CanvasWidth = 640;
	public float CanvasHeight = 480;
	public Double GazeConfidence = 0.7;

	private Boolean isVRmode = false;

	int _gazeFPS = 0;
	int _currentFps = 0;
	DateTime _lastT;

	object _dataLock;

	public int FPS {
		get{ return _currentFps; }
	}

	enum EStatus
	{
		Idle,
		ProcessingGaze,
		Calibration
	}

	EStatus m_status = EStatus.Idle;

	public enum GazeSource
	{
		LeftEye,
		RightEye,
		BothEyes
	}

	public Vector2 NormalizedEyePos {
		get{ return _eyePos; }
	}

	public Vector2 EyePos {
		get{ return new Vector2 ((_eyePos.x - 0.5f) * CanvasWidth, (_eyePos.y - 0.5f) * CanvasHeight); }
	}

	public Vector2 LeftEyePos {
		get{ return leftEye.gaze == null ? Vector2.zero : leftEye.gaze; }
	}

	public Vector2 RightEyePos {
		get{ return rightEye.gaze == null ? Vector2.zero : rightEye.gaze; }
	}

	public Vector2 GetEyeGaze (GazeSource s)
	{
		if (s == GazeSource.RightEye)
			return RightEyePos;
		if (s == GazeSource.LeftEye)
			return LeftEyePos;
		return NormalizedEyePos;
	}

	public PupilGazeTracker ()
	{
		_Instance = this;
	}

	void Start ()
	{
		if (PupilGazeTracker._Instance == null)
			PupilGazeTracker._Instance = this;
		leftEye = new EyeData (SamplesCount);
		rightEye = new EyeData (SamplesCount);

		#if UNITY_EDITOR
		isVRmode = PlayerSettings.virtualRealitySupported;
		#endif

		_dataLock = new object ();
		udpsocketScript = camera.GetComponent<udpsocket> ();
		_serviceThread = new Thread (NetMQClient);
		_serviceThread.Start ();

	}

	void OnDestroy ()
	{
		if (m_status == EStatus.Calibration)
			StopCalibration ();
		_isDone = true;
		_serviceThread.Join ();
	}

	NetMQMessage _sendRequestMessage (Dictionary<string,object> data)
	{
		try {
			NetMQMessage m = new NetMQMessage ();
			m.Append ("notify." + data ["subject"]);

			using (var byteStream = new MemoryStream ()) {
				var ctx = new SerializationContext ();
				ctx.CompatibilityOptions.PackerCompatibilityOptions = MsgPack.PackerCompatibilityOptions.None;
				var ser = MessagePackSerializer.Get<object> (ctx);
				ser.Pack (byteStream, data);
				m.Append (byteStream.ToArray ());
			}

			_requestSocket.SendMultipartMessage (m);

			NetMQMessage recievedMsg;
			recievedMsg = _requestSocket.ReceiveMultipartMessage ();

			return recievedMsg;
		} catch (Exception e) {
			Debug.LogWarning ("NO MQ CONNECTION " + e.Message);
            
			return null;
		}
	}

	float GetPupilTimestamp ()
	{
		_requestSocket.SendFrame ("t");
		NetMQMessage recievedMsg = _requestSocket.ReceiveMultipartMessage ();
		return float.Parse (recievedMsg [0].ConvertToString ());
	}

	void NetMQClient ()
	{
		Debug.Log ("Inside NetMQClient");
		//thanks for Yuta Itoh sample code to connect via NetMQ with Pupil Service
		string IPHeader = ">tcp://" + ServerIP + ":";
		var timeout = new System.TimeSpan (0, 0, 1); //1sec

		// Necessary to handle this NetMQ issue on Unity editor
		// https://github.com/zeromq/netmq/issues/526
		AsyncIO.ForceDotNet.Force ();
		NetMQConfig.ManualTerminationTakeOver ();
		NetMQConfig.ContextCreate (true);

		string subport = "";
		Debug.Log ("Connect to the server: " + IPHeader + ServicePort + ".");
		_requestSocket = new RequestSocket (IPHeader + ServicePort);

		_requestSocket.SendFrame ("SUB_PORT");
		_isconnected = _requestSocket.TryReceiveFrameString (timeout, out subport);

		_lastT = DateTime.Now;

		if (_isconnected) {
			StartProcess ();
			var subscriberSocket = new SubscriberSocket (IPHeader + subport);
			subscriberSocket.Subscribe ("gaze"); //subscribe for gaze data
			subscriberSocket.Subscribe ("notify."); //subscribe for all notifications
			subscriberSocket.Subscribe ("surface"); //subscribe to gaze on surface
			_setStatus (EStatus.ProcessingGaze);
			var msg = new NetMQMessage ();

			while (_isDone == false) {
				_isconnected = subscriberSocket.TryReceiveMultipartMessage (timeout, ref(msg));
				if (_isconnected) {
					try {
						string msgType = msg [0].ConvertToString ();

						if (msgType == "surface" && !isVRmode) {
							var message = MsgPack.Unpacking.UnpackObject (msg [1].ToByteArray ());
							MsgPack.MessagePackObject mmap = message.Value;
							lock (_dataLock) {
								_pupilGazeOnSurface = JsonUtility.FromJson<Pupil.PupilGazeOnSurface> (mmap.ToString ());

								//Debug.Log(mmap.ToString());
								foreach (Pupil.GazeOnSrf gazeData in _pupilGazeOnSurface.gaze_on_srf) {
									//Debug.Log("confidence: " + gazeData.confidence);
									if (gazeData.confidence > GazeConfidence && gazeData.on_srf) {
										Vector2 norm = new Vector2 ((float)gazeData.norm_pos [0], (float)gazeData.norm_pos [1]);
										//Debug.Log("norm pos: " + norm);
										udpsocketScript.processingList.Add (norm);
									}
								}
							}
						}
						if (msgType == "gaze" && isVRmode) {
							var message = MsgPack.Unpacking.UnpackObject (msg [1].ToByteArray ());
							MsgPack.MessagePackObject mmap = message.Value;
							lock (_dataLock) {

								_pupilData = JsonUtility.FromJson<Pupil.PupilData3D> (mmap.ToString ());
								Debug.Log (_pupilData);
								if (_pupilData.confidence > GazeConfidence) {
									OnPacket (_pupilData);
								}
							}
						}
						//Debug.Log(message);
					} catch {
						//	Debug.Log("Failed to unpack.");
					}
				} else {
					//	Debug.Log("Failed to receive a message.");
					Thread.Sleep (500);
				}
			}

			StopProcess ();

			subscriberSocket.Close ();
		} else {
			Debug.Log ("Failed to connect the server.");
		}

		_requestSocket.Close ();
		// Necessary to handle this NetMQ issue on Unity editor
		// https://github.com/zeromq/netmq/issues/526
		Debug.Log ("ContextTerminate.");
		NetMQConfig.ContextTerminate ();
	}

	void _setStatus (EStatus st)
	{
		if (st == EStatus.Calibration) {
			_calibrationData.Clear ();
			_currCalibPoint = 0;
			_currCalibSamples = 0;
		}

		m_status = st;
	}

	public void StartProcess ()
	{
		_sendRequestMessage (new Dictionary<string,object> { { "subject","eye_process.should_start.0" }, { "eye_id",0 } });
		_sendRequestMessage (new Dictionary<string,object> { { "subject","eye_process.should_start.1" }, { "eye_id",1 } });
	}

	public void StopProcess ()
	{
		_sendRequestMessage (new Dictionary<string,object> { { "subject","eye_process.should_stop" }, { "eye_id",0 } });
		_sendRequestMessage (new Dictionary<string,object> { { "subject","eye_process.should_stop" }, { "eye_id",1 } });
	}

	public void StartCalibration ()
	{
		//calibrate using default 9 points and 120 samples for each target
		StartCalibration (new Vector2[] {new Vector2 (0.5f, 0.5f), new Vector2 (0.2f, 0.2f), new Vector2 (0.2f, 0.5f),
			new Vector2 (0.2f, 0.8f), new Vector2 (0.5f, 0.8f), new Vector2 (0.8f, 0.8f),
			new Vector2 (0.8f, 0.5f), new Vector2 (0.8f, 0.2f), new Vector2 (0.5f, 0.2f)
		}, DefaultCalibrationCount);
	}

	public void StartCalibration (Vector2[] calibPoints, int samples)
	{
		_calibPoints = calibPoints;
		_calibSamples = samples;
		Debug.Log ("Inside StartCalibration Fn");
		_sendRequestMessage (new Dictionary<string,object> { { "subject","start_plugin" }, { "name","HMD_Calibration" } });
		_sendRequestMessage (new Dictionary<string,object> {
			{ "subject","calibration.should_start" },
			 {
				"hmd_video_frame_size",
				new float[] {
					1000,
					1000
				}
			},
			 {
				"outlier_threshold",
				35
			}
		});
		_setStatus (EStatus.Calibration);

		if (OnCalibrationStarted != null)
			OnCalibrationStarted (this);
	}

	public void StopCalibration ()
	{
		_sendRequestMessage (new Dictionary<string,object> { { "subject","calibration.should_stop" } });
		if (OnCalibrationDone != null)
			OnCalibrationDone (this);
		_setStatus (EStatus.ProcessingGaze);
	}


	void _CalibData (float x, float y)
	{
		if (OnCalibData != null)
			OnCalibData (this, x, y);
	}


	void OnPacket (Pupil.PupilData3D data)
	{
		//add new frame
		_gazeFPS++;
		var ct = DateTime.Now;
		if ((ct - _lastT).TotalSeconds > 1) {
			_lastT = ct;
			_currentFps = _gazeFPS;
			_gazeFPS = 0;
		}

		if (m_status == EStatus.ProcessingGaze) { //gaze processing stage

			float x, y;
			x = (float)data.norm_pos [0];
			y = (float)data.norm_pos [1];
			_eyePos.x = (leftEye.gaze.x + rightEye.gaze.x) * 0.5f;
			_eyePos.y = (leftEye.gaze.y + rightEye.gaze.y) * 0.5f;
			if (data.id == 0) {
				leftEye.AddGaze (x, y);
				if (OnEyeGaze != null)
					OnEyeGaze (this);
			} else if (data.id == 1) {
				rightEye.AddGaze (x, y);
				if (OnEyeGaze != null)
					OnEyeGaze (this);
			}


		} else if (m_status == EStatus.Calibration) {//gaze calibration stage
			float t = GetPupilTimestamp ();
			var ref0 = new Dictionary<string,object> () {
				 {
					"norm_pos",
					new float[] {
						_calibPoints [_currCalibPoint].x,
						_calibPoints [_currCalibPoint].y
					}
				},
				 {
					"timestamp",
					t
				},
				 {
					"id",
					0
				}
			};
			var ref1 = new Dictionary<string,object> () {
				 {
					"norm_pos",
					new float[] {
						_calibPoints [_currCalibPoint].x,
						_calibPoints [_currCalibPoint].y
					}
				},
				 {
					"timestamp",
					t
				},
				 {
					"id",
					1
				}
			};

			_CalibData (_calibPoints [_currCalibPoint].x, _calibPoints [_currCalibPoint].y);

			_calibrationData.Add (ref0);
			_calibrationData.Add (ref1);
			_currCalibSamples++;
			Thread.Sleep (1000 / 60);

			if (_currCalibSamples >= _calibSamples) {
				_currCalibSamples = 0;
				_currCalibPoint++;

				string pointsData = "[";
				int index = 0;
				foreach (var v in _calibrationData) {
					pointsData += JsonUtility.ToJson (v);//String.Format("{'norm_pos':({0},{1}),'timestamp':{2},'id':{3}}",v.norm_pos[0],v.norm_pos[1],v.timestamp,v.id);
					++index;
					if (index != _calibrationData.Count) {
						pointsData += ",";
					}
				}
				pointsData += "]";
				//	pointsData = JsonUtility.ToJson (_CalibrationPoints);
				//Debug.Log (pointsData);

				_sendRequestMessage (new Dictionary<string,object> {
					{ "subject","calibration.add_ref_data" },
					 {
						"ref_data",
						_CalibrationPoints
					}
				});
				_calibrationData.Clear ();
				if (_currCalibPoint >= _calibPoints.Length) {

					StopCalibration ();
				}
			}
		}
	}


}
