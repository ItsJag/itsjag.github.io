import cv2
import pykitti
import matplotlib
import numpy as np
from torch import Tensor

from ultralytics import YOLO

import r2d2_interface as r2

# Thanks to Nicolai Nielsen for the amazing and thorough introduction to the subject.

# Copyright (c) 2023 Jo Georgeson
# Licensed under the BSD 3-Clause Licence

# Image filepath
kitti_dir = "../datasets/"

# A class that uses existing KITTI sequences to showcase approaches in Monocular Visual Odometry
class monoVO():

    #List of detectors and matchers usable within class
    DETECTOR_ORB = 1
    DETECTOR_R2D2 = 2

    MATCHER_FLANN = 1
    MATCHER_BF = 2

    def __init__(self, sequence="00", max_frames=300, n_features=3000, detector= DETECTOR_ORB, matcher=MATCHER_FLANN, show_matches=True, yolo_filtering=False):
        self.data = pykitti.odometry(kitti_dir, str(sequence), frames=range(0,max_frames))
        self.orb = cv2.ORB_create(n_features)
        self.fast = cv2.FastFeatureDetector_create(cv2.NORM_HAMMING)
        self.detector = detector
        self.matcher = matcher
        self.show_matches = show_matches
        self.n_features = n_features

        if self.detector == self.DETECTOR_R2D2:
            index_par = dict(algorithm=1, trees=5)
            search_par = dict(checks=50)
        else:
            index_par = dict(algorithm=6, table_number=6, key_size=12, multi_probe_level=1)
            search_par = dict(checks=50)

        self.flann = cv2.FlannBasedMatcher(indexParams=index_par, searchParams=search_par)
        self.bf = cv2.BFMatcher()
        self.ground_poses = self.data.poses

        matplotlib.use("TkAgg")
        if yolo_filtering:
            self.yolo = YOLO("yolov8n.pt")
        self.yolo_filtering = yolo_filtering


    def get_image(self, i:int):
        """Returns image n from the KITTI sequence given at initialisation"""
        return self.data.get_cam0(i)


    
    @staticmethod
    def trans_mat(R, t):
        """Create a transformation matrix from a translation and rotation matrix."""

        T = np.eye(4, dtype=np.float64)
        T[:3, :3] = R
        T[:3, 3] = t
        return T
    

    def match(self, i):
        """Detect and match features between image i-1 and image i in data.get_cam0, and return all 'good' matches through q1 and q2."""

        img1_raw = self.data.get_cam0(i - 1)
        img2_raw = self.data.get_cam0(i)
        img1 = np.array(img1_raw)
        img2 = np.array(img2_raw)

        # Filter Keypoints using YOLO (if enabled)
        if self.yolo_filtering:
            img_mask1 = self.yolo_filter(img1_raw)
            img_mask2 = self.yolo_filter(img2_raw)
        else:
            img_mask1 = None
            img_mask2 = None

        #Select feature detector
        match self.detector:
            case self.DETECTOR_ORB:
                kp1, des1 = self.orb.detectAndCompute(img1, img_mask1)
                kp2, des2 = self.orb.detectAndCompute(img2, img_mask2)
            case self.DETECTOR_R2D2:
                temp_kp1, des1 = r2.extract_keypoints(img1, "r2d2/models/faster2d2_WASF_N16.pt", self.n_features)
                temp_kp2, des2 = r2.extract_keypoints(img2, "r2d2/models/faster2d2_WASF_N16.pt", self.n_features)
                kp1 = []
                kp2 = []
                for pt in temp_kp1:
                    kp1.append(cv2.KeyPoint(pt[0], pt[1], pt[2], 0))
                for pt in temp_kp2:
                    kp2.append(cv2.KeyPoint(pt[0], pt[1], pt[2], 0))
            
            case _: # Use ORB by default
                kp1, des1 = self.orb.detectAndCompute(img1, img_mask1)
                kp2, des2 = self.orb.detectAndCompute(img2, img_mask2)





        #Select feature matcher
        if self.matcher == self.MATCHER_FLANN:
            matches = self.flann.knnMatch(des1,des2,k=2)
        else:
            matches = self.bf.knnMatch(des1,des2,k=2)

        good = []

        # Perform Ratio test for good matches
        try:
            for m, n in matches:
                if m.distance < 0.8 * n.distance:
                    good.append(m)
        except ValueError:
            pass
        
        # Display the matches
        if self.show_matches:
            draw_params = dict(matchColor = -1, singlePointColor = None, matchesMask = None, flags = 2)
            img3 = cv2.drawMatches(img1, kp1, img2, kp2, good, None, **draw_params)
            cv2.imshow("image", img3) # Depending on system performance the image might not show, but don't worry, if you use cv2.waitKey() you'll see it's working as intended.
            cv2.waitKey(1)

        q1 = np.float32([kp1[m.queryIdx].pt for m in good])
        q2 = np.float32([kp2[m.trainIdx].pt for m in good])
        return q1, q2


    def calculate_scale(self, R,t, q1, q2):
        """Function that takes in rotation, translation matrices and matches to output estimate for Transform and estimate for scale."""
        T = self.trans_mat(R,t) # Find transform
        K = self.data.calib.K_cam0 # Grab calibration data
        P = np.matmul(np.concatenate((K, np.zeros((3,1))), axis=1), T) 
        proj_mat = self.data.calib.P_rect_00

        hom_q1 = cv2.triangulatePoints(proj_mat, P, q1.T, q2.T)
        hom_q2 = np.matmul(T, hom_q1)

        un_hom_q1 = hom_q1[:3,:] / hom_q1[3,:]
        un_hom_q2 = hom_q2[:3,:] / hom_q2[3,:]

        pos_z_q1 = sum(un_hom_q1[2,:] > 0)
        pos_z_q2 = sum(un_hom_q2[2,:] > 0)

        relative_scale = np.mean(np.linalg.norm(un_hom_q1.T[:-1] - un_hom_q1.T[1:], axis=-1)/ np.linalg.norm(un_hom_q2.T[:-1] - un_hom_q2.T[1:], axis=-1))
        return pos_z_q1 + pos_z_q2, relative_scale
    
    def use_absolute_scale(self, R, t, q1, q2, q2_idx):
        """Function that takes in rotation, translation matrices and matches to output estimate for Transform and exact scale gathered from Ground Truth."""
        T = self.trans_mat(R,t)
        K = self.data.calib.K_cam0
        P = np.matmul(np.concatenate((K, np.zeros((3,1))), axis=1), T)
        proj_mat = self.data.calib.P_rect_00

        hom_q1 = cv2.triangulatePoints(proj_mat, P, q1.T, q2.T)
        hom_q2 = np.matmul(T, hom_q1)

        un_hom_q1 = hom_q1[:3,:] / hom_q1[3,:]
        un_hom_q2 = hom_q2[:3,:] / hom_q2[3,:]

        pos_z_q1 = sum(un_hom_q1[2,:] > 0)
        pos_z_q2 = sum(un_hom_q2[2,:] > 0)

        gt_q1 = self.ground_poses[q2_idx - 1]
        gt_q2 = self.ground_poses[q2_idx]

        scale = np.sqrt((gt_q2[0,3] - gt_q1[0,3])**2 + (gt_q2[2,3] - gt_q1[2,3])**2)
        return pos_z_q1 + pos_z_q2, scale

    def decomp_essential_mat(self, E, q1, q2):
        """Function that decomposes the essential matrix and returns the estimate for rotation and translation (using estimated scale)"""
        R1, R2, t = cv2.decomposeEssentialMat(E)
        t = np.squeeze(t)

        pairs = [[R1, t], [R1, -t], [R2, t], [R2, -t]]

        z_sums = []
        relative_scales = []
        for R, t in pairs:
            z_sum, scale = self.calculate_scale(R, t, q1, q2)
            z_sums.append(z_sum)
            relative_scales.append(scale)
        
        right_pair_idx = np.argmax(z_sums)
        right_pair = pairs[right_pair_idx]
        relative_scale = relative_scales[right_pair_idx]
        R1, t = right_pair
        t = t * relative_scale

        return [R1, t]
    
    def decomp_essential_mat_abs_scale(self, E, q1, q2, q2_idx):
        """Function that decomposes the essential matrix and returns the estimate for rotation and translation (using ground truth scale)"""
        R1, R2, t = cv2.decomposeEssentialMat(E)
        t = np.squeeze(t)

        pairs = [[R1, t], [R1, -t], [R2, t], [R2, -t]]

        z_sums = []
        relative_scales = []
        for R, t in pairs:
            z_sum, scale = self.use_absolute_scale(R, t, q1, q2, q2_idx)
            z_sums.append(z_sum)
            relative_scales.append(scale)
        
        right_pair_idx = np.argmax(z_sums)
        right_pair = pairs[right_pair_idx]
        relative_scale = relative_scales[right_pair_idx]
        R1, t = right_pair
        t = t * relative_scale

        return [R1, t]

    
    def get_pose(self, q1, q2):

        K = self.data.calib.K_cam0
        E, _ = cv2.findEssentialMat(q1,q2,K,threshold=0.5)
        
        R,t = self.decomp_essential_mat(E,q1,q2)

        T = self.trans_mat(R,np.squeeze(t))
        return T
    
    def get_pose_abs_scale(self, q1, q2, q2_idx):
        K = self.data.calib.K_cam0
        E, _ = cv2.findEssentialMat(q1,q2,K,threshold=0.5)
        
        R,t = self.decomp_essential_mat_abs_scale(E,q1,q2, q2_idx)

        T = self.trans_mat(R,np.squeeze(t))
        return T
    
    def yolo_filter(self, img):
        """Uses YOLO object detection to mask out keypoints on objects likely to be moving in the environment."""

        # Detect objects in image
        res = self.yolo.predict(img, half=True, max_det=5, verbose=False)
        np_img = np.array(img)
        img_mask = np.full(np_img.shape, 255, dtype="uint8")

        for box in res[0].boxes:
            box_cpy = Tensor.cpu(box.xyxy) # Copy to CPU memory since it (may) run on GPU
            (start_x, start_y, end_x, end_y) = box_cpy.numpy().astype(int)[0]

            img_mask[start_y:end_y, start_x:end_x] = 0
        

        return img_mask